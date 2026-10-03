using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

internal sealed class DefaultOrchestrationRecoveryService : IOrchestrationRecoveryService
{
    private const string ReplayRequestedOnUtcMetadataKey = "Recovery.ReplayRequestedOnUtc";
    private const string ReplayRequestedByMetadataKey = "Recovery.ReplayRequestedBy";
    private const string AbortReasonMetadataKey = "Recovery.AbortReason";
    private const string AbortedOnUtcMetadataKey = "Recovery.AbortedOnUtc";

    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRuntimeArtifactResolver _artifactResolver;
    private readonly ITaskAttemptDispatcher _taskAttemptDispatcher;
    private readonly ISagaEngine _sagaEngine;

    public DefaultOrchestrationRecoveryService(
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        ITaskExecutionRepository taskRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeArtifactResolver artifactResolver,
        ITaskAttemptDispatcher taskAttemptDispatcher,
        ISagaEngine sagaEngine)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
        _taskAttemptDispatcher = taskAttemptDispatcher ?? throw new ArgumentNullException(nameof(taskAttemptDispatcher));
        _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
    }

    public async Task<OrchestrationRecoveryResult> ReplayAsync(
        string instanceId,
        string payload = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseId(instanceId, out var parsedInstanceId))
        {
            return OrchestrationRecoveryResult.Rejected(instanceId, string.Empty, "Instance id is invalid.");
        }

        var now = DateTime.UtcNow;
        var instance = await _instanceRepository.GetById(parsedInstanceId, cancellationToken);
        if (!IsRecoverable(instance.Status))
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                instance.Status.ToString(),
                $"Instance '{instance.Id}' is not in a recoverable status.");
        }

        var resolvedArtifact = await _artifactResolver.ResolveAsync(
            instance.RuntimeOrchestrationArtifactId.ToString(),
            cancellationToken);
        var stages = (resolvedArtifact.Artifact.StageDefinitions ?? [])
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)
            .ToArray();
        var stageExecutions = await _stageRepository.GetByInstanceId(instance.Id, cancellationToken);
        var taskExecutions = await _taskRepository.GetByInstanceId(instance.Id, cancellationToken);
        var failedTask = taskExecutions
            .Where(x => x.Status is TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut)
            .OrderByDescending(x => x.FailedOnUtc ?? x.TimedOutOnUtc ?? x.StartedOnUtc ?? DateTime.MinValue)
            .FirstOrDefault();

        if (failedTask is not null)
        {
            return await ReplayTaskAsync(
                instance,
                failedTask,
                stageExecutions,
                stages,
                resolvedArtifact.Artifact.MetadataDescriptors,
                payload,
                now,
                cancellationToken);
        }

        var failedStage = stageExecutions
            .Where(x => x.Status == StageExecutionStatus.Failed)
            .OrderByDescending(x => x.FailedOnUtc ?? x.StartedOnUtc)
            .FirstOrDefault();
        if (failedStage is not null)
        {
            return await ReplayStageAsync(
                instance,
                failedStage,
                payload,
                now,
                cancellationToken);
        }

        instance.Status = OrchestrationInstanceStatus.Running;
        instance.FailedOnUtc = null;
        instance.ErrorSummary = null;
        instance.WaitingSinceUtc = null;
        instance.LastUpdatedOnUtc = now;
        instance.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        instance.Metadata[ReplayRequestedByMetadataKey] = JsonValue.Create("operator");
        await _instanceRepository.Update(instance, cancellationToken);
        await CreateReplayTransition(instance, null, null, "Instance replay requested.", now, cancellationToken);
        await _sagaEngine.OrchestrateAsync(BuildForwardIntent(instance, payload), cancellationToken);

        return OrchestrationRecoveryResult.Success(
            instance.Id.ToString(),
            OrchestrationInstanceStatus.Running.ToString(),
            "Replay requested for recoverable orchestration instance.");
    }

    public async Task<OrchestrationRecoveryResult> AbortAsync(
        string instanceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseId(instanceId, out var parsedInstanceId))
        {
            return OrchestrationRecoveryResult.Rejected(instanceId, string.Empty, "Instance id is invalid.");
        }

        var now = DateTime.UtcNow;
        var instance = await _instanceRepository.GetById(parsedInstanceId, cancellationToken);
        if (IsFinal(instance.Status))
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                instance.Status.ToString(),
                $"Instance '{instance.Id}' is already in a final status.");
        }

        var previousStatus = instance.Status;
        var abortReason = string.IsNullOrWhiteSpace(reason)
            ? "Orchestration instance aborted by operator."
            : reason.Trim();

        instance.Status = OrchestrationInstanceStatus.Aborted;
        instance.FailedOnUtc ??= now;
        instance.StoppedOnUtc ??= now;
        instance.WaitingSinceUtc = null;
        instance.ErrorSummary = abortReason;
        instance.LastUpdatedOnUtc = now;
        instance.Metadata[AbortReasonMetadataKey] = JsonValue.Create(abortReason);
        instance.Metadata[AbortedOnUtcMetadataKey] = JsonValue.Create(now);

        await _instanceRepository.Update(instance, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            TransitionType = "InstanceAborted",
            FromStatus = previousStatus.ToString(),
            ToStatus = OrchestrationInstanceStatus.Aborted.ToString(),
            OccurredOnUtc = now,
            Message = abortReason,
            ProducedBy = nameof(DefaultOrchestrationRecoveryService)
        }, cancellationToken);

        return OrchestrationRecoveryResult.Success(
            instance.Id.ToString(),
            OrchestrationInstanceStatus.Aborted.ToString(),
            abortReason);
    }

    private async Task<OrchestrationRecoveryResult> ReplayTaskAsync(
        OrchestrationInstance instance,
        TaskExecution failedTask,
        IReadOnlyCollection<StageExecution> stageExecutions,
        IReadOnlyCollection<StageArtifact> stages,
        IReadOnlyCollection<MetadataDescriptorArtifact> metadataDescriptors,
        string payload,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var stageExecution = stageExecutions.FirstOrDefault(x => x.Id == failedTask.StageExecutionId);
        var stageArtifact = stages.FirstOrDefault(x =>
            string.Equals(x.Key, stageExecution?.StageKey, StringComparison.OrdinalIgnoreCase));
        var taskArtifact = stageArtifact?.TaskDefinitions?
            .Where(x => x.IsEnabled)
            .FirstOrDefault(x => string.Equals(x.Key, failedTask.TaskKey, StringComparison.OrdinalIgnoreCase));

        if (stageExecution is null || stageArtifact is null || taskArtifact is null)
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                instance.Status.ToString(),
                $"Failed task '{failedTask.TaskKey}' cannot be replayed because its artifact definition is unavailable.");
        }

        failedTask.Metadata.Remove("RetrySuppressed");
        failedTask.Metadata["ManualReplay"] = JsonValue.Create(true);
        failedTask.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        instance.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        instance.Metadata[ReplayRequestedByMetadataKey] = JsonValue.Create("operator");

        await _taskAttemptDispatcher.DispatchAsync(
            new TaskAttemptDispatchRequest
            {
                Kind = TaskAttemptDispatchKind.Retry,
                Instance = instance,
                StageExecutionId = stageExecution.Id,
                StageKey = stageArtifact.Key,
                Task = taskArtifact,
                TaskExecution = failedTask,
                Payload = string.IsNullOrWhiteSpace(payload) ? instance.SnapshotPayload?.ToJsonString() : payload,
                MetadataDescriptors = metadataDescriptors,
                NowUtc = now
            },
            cancellationToken);

        await CreateReplayTransition(
            instance,
            stageExecution.Id,
            failedTask.Id,
            $"Replay requested for task '{failedTask.TaskKey}'.",
            now,
            cancellationToken);

        return OrchestrationRecoveryResult.Success(
            instance.Id.ToString(),
            OrchestrationInstanceStatus.Running.ToString(),
            $"Replay requested for task '{failedTask.TaskKey}'.");
    }

    private async Task<OrchestrationRecoveryResult> ReplayStageAsync(
        OrchestrationInstance instance,
        StageExecution failedStage,
        string payload,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var previousStatus = failedStage.Status;
        failedStage.Status = StageExecutionStatus.Running;
        failedStage.FailedOnUtc = null;
        failedStage.ErrorSummary = null;
        failedStage.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);

        instance.Status = OrchestrationInstanceStatus.Running;
        instance.CurrentStageKey = failedStage.StageKey;
        instance.CurrentTaskKey = string.Empty;
        instance.FailedOnUtc = null;
        instance.ErrorSummary = null;
        instance.WaitingSinceUtc = null;
        instance.LastUpdatedOnUtc = now;
        instance.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        instance.Metadata[ReplayRequestedByMetadataKey] = JsonValue.Create("operator");

        await _stageRepository.Update(failedStage, cancellationToken);
        await _instanceRepository.Update(instance, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = failedStage.Id,
            TransitionType = "StageReplayRequested",
            FromStatus = previousStatus.ToString(),
            ToStatus = StageExecutionStatus.Running.ToString(),
            OccurredOnUtc = now,
            Message = $"Replay requested for stage '{failedStage.StageKey}'.",
            ProducedBy = nameof(DefaultOrchestrationRecoveryService)
        }, cancellationToken);

        await _sagaEngine.OrchestrateAsync(BuildForwardIntent(instance, payload), cancellationToken);

        return OrchestrationRecoveryResult.Success(
            instance.Id.ToString(),
            OrchestrationInstanceStatus.Running.ToString(),
            $"Replay requested for stage '{failedStage.StageKey}'.");
    }

    private async Task CreateReplayTransition(
        OrchestrationInstance instance,
        Id? stageExecutionId,
        Id? taskExecutionId,
        string message,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = stageExecutionId,
            TaskExecutionId = taskExecutionId,
            TransitionType = "InstanceReplayRequested",
            FromStatus = OrchestrationInstanceStatus.DeadLettered.ToString(),
            ToStatus = OrchestrationInstanceStatus.Running.ToString(),
            OccurredOnUtc = now,
            Message = message,
            ProducedBy = nameof(DefaultOrchestrationRecoveryService)
        }, cancellationToken);
    }

    private static ForwardIntent BuildForwardIntent(OrchestrationInstance instance, string payload)
        => new()
        {
            ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
            MessageMetadata = new Abstractions.Runtime.Metadata.OrchestrationMessageMetadata
            {
                SagaId = string.IsNullOrWhiteSpace(instance.SagaId) ? instance.Id.ToString() : instance.SagaId,
                OrchestrationInstanceId = instance.Id.ToString(),
                CurrentStage = instance.CurrentStageKey,
                CurrentTasks = string.IsNullOrWhiteSpace(instance.CurrentTaskKey) ? [] : [instance.CurrentTaskKey],
                CorrelationId = instance.CorrelationId
            },
            Payload = string.IsNullOrWhiteSpace(payload)
                ? instance.SnapshotPayload?.DeepClone()
                : JsonNode.Parse(payload)
        };

    private static bool IsRecoverable(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Failed
            or OrchestrationInstanceStatus.DeadLettered
            or OrchestrationInstanceStatus.Running
            or OrchestrationInstanceStatus.Waiting;

    private static bool IsFinal(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Completed
            or OrchestrationInstanceStatus.CompletedWithErrors
            or OrchestrationInstanceStatus.Compensated
            or OrchestrationInstanceStatus.Aborted
            or OrchestrationInstanceStatus.Stopped;

    private static bool TryParseId(string value, out Id id)
    {
        id = default;
        if (Ulid.TryParse(value, out var parsed))
        {
            id = new Id(parsed);
            return true;
        }

        return false;
    }
}
