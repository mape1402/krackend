using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

internal sealed class DefaultOrchestrationRecoveryService : IOrchestrationRecoveryService
{
    private const string ReplayRequestedOnUtcMetadataKey = "Recovery.ReplayRequestedOnUtc";
    private const string ReplayRequestedByMetadataKey = "Recovery.ReplayRequestedBy";
    private const string TaskReplayReasonMetadataKey = "Recovery.TaskReplayReason";
    private const string DispatchReplayRequestIdMetadataKey = "Recovery.DispatchReplayRequestId";
    private const string DispatchReplayRequestedOnUtcMetadataKey = "Recovery.DispatchReplayRequestedOnUtc";
    private const string DispatchReplayReasonMetadataKey = "Recovery.DispatchReplayReason";
    private const string DispatchReplayPreviousInstanceStatusMetadataKey = "Recovery.DispatchReplayPreviousInstanceStatus";
    private const string DispatchReplayPreviousTaskStatusMetadataKey = "Recovery.DispatchReplayPreviousTaskStatus";
    private const string DispatchReplayPreviousAttemptStatusMetadataKey = "Recovery.DispatchReplayPreviousAttemptStatus";
    private const string DispatchReplayProviderStatusMetadataKey = "Recovery.DispatchReplayProviderStatus";
    private const string DispatchReplayProviderActionIdMetadataKey = "Recovery.DispatchReplayProviderActionId";
    private const string AbortReasonMetadataKey = "Recovery.AbortReason";
    private const string AbortedOnUtcMetadataKey = "Recovery.AbortedOnUtc";

    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly IRuntimeArtifactResolver _artifactResolver;
    private readonly ITaskAttemptDispatcher _taskAttemptDispatcher;
    private readonly ITaskDispatchReplayService _taskDispatchReplayService;
    private readonly IOrchestrationDispatchCancellationCoordinator _dispatchCancellationCoordinator;
    private readonly ISagaEngine _sagaEngine;
    private readonly IOrchestrationInstanceCoordinator _coordinator;
    private readonly IRuntimeStorageUnitOfWork _storageUnitOfWork;
    private readonly ArchivedRecoveryIngressPreparer _archivedIngressPreparer;
    private readonly OrchestrationTimeoutOptions _timeoutOptions;
    private readonly IOrchestrationReconciliationService _reconciliation;

    public DefaultOrchestrationRecoveryService(
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        ITaskExecutionRepository taskRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeArtifactResolver artifactResolver,
        ITaskAttemptDispatcher taskAttemptDispatcher,
        ISagaEngine sagaEngine,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        ITaskDispatchReplayService taskDispatchReplayService,
        IRuntimeStorageUnitOfWork storageUnitOfWork,
        IOrchestrationDispatchCancellationCoordinator dispatchCancellationCoordinator,
        IOrchestrationInstanceCoordinator coordinator = null,
        ArchivedRecoveryIngressPreparer archivedIngressPreparer = null,
        IOptions<OrchestrationTimeoutOptions> timeoutOptions = null,
        IOrchestrationReconciliationService reconciliation = null)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
        _taskAttemptDispatcher = taskAttemptDispatcher ?? throw new ArgumentNullException(nameof(taskAttemptDispatcher));
        _taskDispatchReplayService = taskDispatchReplayService ?? throw new ArgumentNullException(nameof(taskDispatchReplayService));
        _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
        _storageUnitOfWork = storageUnitOfWork ?? throw new ArgumentNullException(nameof(storageUnitOfWork));
        _dispatchCancellationCoordinator = dispatchCancellationCoordinator ?? throw new ArgumentNullException(nameof(dispatchCancellationCoordinator));
        _coordinator = coordinator ?? PassthroughOrchestrationInstanceCoordinator.Instance;
        _archivedIngressPreparer = archivedIngressPreparer;
        _timeoutOptions = timeoutOptions?.Value ?? new OrchestrationTimeoutOptions();
        _reconciliation = reconciliation;
    }

    public async Task<OrchestrationRecoveryResult> ReplayAsync(
        string instanceId,
        string payload = null,
        CancellationToken cancellationToken = default)
        => await RecoverAsync(instanceId, payload, terminalOnly: false, cancellationToken);

    /// <inheritdoc />
    public async Task<OrchestrationRecoveryResult> RecoverTerminalAsync(
        string instanceId,
        string payload = null,
        CancellationToken cancellationToken = default)
        => await RecoverAsync(instanceId, payload, terminalOnly: true, cancellationToken);

    private async Task<OrchestrationRecoveryResult> RecoverAsync(
        string instanceId,
        string payload,
        bool terminalOnly,
        CancellationToken cancellationToken)
    {
        if (!TryParseId(instanceId, out var parsedInstanceId))
        {
            return OrchestrationRecoveryResult.Rejected(instanceId, string.Empty, "Instance id is invalid.");
        }

        OrchestrationRecoveryResult result = null;
        var acquired = await _coordinator.TryExecuteAsync(
            parsedInstanceId,
            async token => result = await ReplayCoreAsync(parsedInstanceId, payload, terminalOnly, token),
            cancellationToken);

        return acquired
            ? result
            : OrchestrationRecoveryResult.Rejected(
                parsedInstanceId.ToString(),
                string.Empty,
                $"Instance '{parsedInstanceId}' is being processed by another runtime node.");
    }

    private async Task<OrchestrationRecoveryResult> ReplayCoreAsync(
        Id parsedInstanceId,
        string payload,
        bool terminalOnly,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var instance = await _instanceRepository.GetById(parsedInstanceId, cancellationToken);
        if (instance is null)
        {
            return OrchestrationRecoveryResult.Rejected(
                parsedInstanceId.ToString(),
                string.Empty,
                $"Instance '{parsedInstanceId}' was not found.");
        }

        if (instance.Status == OrchestrationInstanceStatus.Completed)
            return await RecoverIncorrectlyCompletedParallelAsync(instance, payload, cancellationToken);

        if (!IsRecoverable(instance.Status) || (terminalOnly && !IsOperatorRecoverableTerminal(instance.Status)))
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                instance.Status.ToString(),
                $"Instance '{instance.Id}' is not in a recoverable status.");
        }

        var preparationFailure = await PrepareArchivedIngressAsync(instance, cancellationToken);
        if (preparationFailure is not null) return preparationFailure;
        var resolvedArtifact = await _artifactResolver.ResolveAsync(
            instance.RuntimeOrchestrationArtifactId.ToString(),
            cancellationToken);
        EnsureProjectedPolicy(instance, resolvedArtifact.Artifact);
        var stages = (resolvedArtifact.Artifact.StageDefinitions ?? [])
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)
            .ToArray();
        var stageExecutions = await _stageRepository.GetByInstanceId(instance.Id, cancellationToken);
        var taskExecutions = await _taskRepository.GetByInstanceId(instance.Id, cancellationToken);
        var failures = taskExecutions
            .Where(x => x.Status is TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut)
            .OrderByDescending(x => x.FailedOnUtc ?? x.TimedOutOnUtc ?? x.StartedOnUtc ?? DateTime.MinValue)
            .ToArray();
        var failedTask = failures.FirstOrDefault(task =>
        {
            var stage = stageExecutions.FirstOrDefault(x => x.Id == task.StageExecutionId);
            var definition = stages.FirstOrDefault(x => string.Equals(x.Key, stage?.StageKey, StringComparison.OrdinalIgnoreCase))
                ?.TaskDefinitions?.FirstOrDefault(x => string.Equals(x.Key, task.TaskKey, StringComparison.Ordinal));
            return !FailureIsAllowedToContinue(task, definition);
        }) ?? failures.FirstOrDefault();

        if (failedTask is not null)
        {
            var cancelled = taskExecutions.Where(x => x.StageExecutionId == failedTask.StageExecutionId &&
                x.Status == TaskExecutionStatus.Cancelled).ToArray();
            var owningStage = stageExecutions.FirstOrDefault(x => x.Id == failedTask.StageExecutionId);
            if (cancelled.Length > 0 && !HasMatchingParallelDefinitions(owningStage, stages, cancelled))
                return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                    "Cancelled parallel tasks do not match the orchestration artifact; inspect the execution before recovery.");
            var parallelFailures = taskExecutions.Where(x => failedTask.ParallelGroupId.HasValue && x.Id != failedTask.Id &&
                x.StageExecutionId == failedTask.StageExecutionId && x.ParallelGroupId == failedTask.ParallelGroupId &&
                x.Status is (TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut)).ToArray();
            if (parallelFailures.Length > 0 && !HasMatchingParallelDefinitions(owningStage, stages, parallelFailures))
                return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                    "Failed parallel tasks do not match the orchestration artifact; inspect the execution before recovery.");
            var stageDefinition = stages.FirstOrDefault(x => string.Equals(x.Key, owningStage?.StageKey, StringComparison.OrdinalIgnoreCase));
            var otherFailures = parallelFailures.Where(task => !FailureIsAllowedToContinue(task,
                stageDefinition?.TaskDefinitions?.FirstOrDefault(definition => definition.Key == task.TaskKey))).ToArray();
            var unsafeCancellation = await PrepareCancelledParallelTasksAsync(instance, cancelled, cancellationToken);
            if (unsafeCancellation is not null) return unsafeCancellation;
            await PrepareOtherParallelFailuresAsync(otherFailures, cancellationToken);

            var replay = await ReplayTaskCoreAsync(
                instance,
                failedTask,
                stageExecutions,
                stages,
                resolvedArtifact.Artifact.MetadataDescriptors,
                payload,
                null,
                now,
                cancellationToken);
            if (!replay.Succeeded) return replay;
            if (cancelled.Length > 0 || otherFailures.Length > 0)
                await _sagaEngine.OrchestrateAsync(BuildForwardIntent(instance, payload), cancellationToken);
            if (failedTask.ParallelGroupId.HasValue && _reconciliation is not null)
                await _reconciliation.ReconcileAsync(instance.Id.ToString(), cancellationToken);
            return replay;
        }

        var failedStage = stageExecutions
            .Where(x => x.Status == StageExecutionStatus.Failed)
            .OrderByDescending(x => x.FailedOnUtc ?? x.StartedOnUtc)
            .FirstOrDefault();
        var currentStage = failedStage ?? stageExecutions.FirstOrDefault(x =>
            string.Equals(x.StageKey, instance.CurrentStageKey, StringComparison.OrdinalIgnoreCase));
        var cancelledWithoutFailure = taskExecutions.Where(x => currentStage is not null &&
            x.StageExecutionId == currentStage.Id &&
            x.Status == TaskExecutionStatus.Cancelled).ToArray();
        if (cancelledWithoutFailure.Length > 0 && !HasMatchingParallelDefinitions(currentStage, stages, cancelledWithoutFailure))
            return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                "Cancelled parallel tasks do not match the orchestration artifact; inspect the execution before recovery.");
        var cancellationFailure = await PrepareCancelledParallelTasksAsync(instance, cancelledWithoutFailure, cancellationToken);
        if (cancellationFailure is not null) return cancellationFailure;
        if (failedStage is not null)
        {
            return await ReplayStageAsync(
                instance,
                failedStage,
                payload,
                now,
                cancellationToken);
        }

        var previousStatus = instance.Status;
        instance.Status = OrchestrationInstanceStatus.Running;
        instance.FailedOnUtc = null;
        instance.ErrorSummary = null;
        instance.WaitingSinceUtc = null;
        instance.IdleSinceUtc = null;
        instance.IdleExpiresOnUtc = null;
        instance.LastUpdatedOnUtc = now;
        instance.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        InstanceLifetimePolicy.ResetDeadline(instance, now);
        instance.Metadata[ReplayRequestedByMetadataKey] = JsonValue.Create("operator");
        await _instanceRepository.Update(instance, cancellationToken);
        await CreateReplayTransition(
            instance,
            null,
            null,
            "InstanceReplayRequested",
            previousStatus.ToString(),
            OrchestrationInstanceStatus.Running.ToString(),
            "Instance replay requested.",
            now,
            cancellationToken);
        await _sagaEngine.OrchestrateAsync(BuildForwardIntent(instance, payload), cancellationToken);
        if (cancelledWithoutFailure.Length > 0 && _reconciliation is not null)
            await _reconciliation.ReconcileAsync(instance.Id.ToString(), cancellationToken);

        return OrchestrationRecoveryResult.Success(
            instance.Id.ToString(),
            OrchestrationInstanceStatus.Running.ToString(),
            "Replay requested for recoverable orchestration instance.");
    }

    private async Task<OrchestrationRecoveryResult> RecoverIncorrectlyCompletedParallelAsync(
        OrchestrationInstance instance, string payload, CancellationToken cancellationToken)
    {
        var taskExecutions = await _taskRepository.GetByInstanceId(instance.Id, cancellationToken);
        var unresolved = taskExecutions.Where(x => x.ParallelGroupId.HasValue &&
            x.Status is TaskExecutionStatus.Cancelled or TaskExecutionStatus.WaitingResponse).ToArray();
        if (unresolved.Length == 0)
            return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                $"Instance '{instance.Id}' is not in a recoverable status.");
        var stageIds = unresolved.Select(x => x.StageExecutionId).Distinct().ToArray();
        if (stageIds.Length != 1 || instance.DispatchCancellationPending)
            return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                "The completed saga has multiple incomplete stages or a pending cancellation; inspect its execution before recovery.");
        var stageExecutions = await _stageRepository.GetByInstanceId(instance.Id, cancellationToken);
        var stage = stageExecutions.FirstOrDefault(x => x.Id == stageIds[0]);
        if (stage is null || stage.Status != StageExecutionStatus.Completed ||
            stageExecutions.Any(x => x.Id != stage.Id && x.Order > stage.Order) ||
            taskExecutions.Any(x => x.StageExecutionId == stage.Id &&
                (x.Status is TaskExecutionStatus.Pending or TaskExecutionStatus.Running or TaskExecutionStatus.Retrying ||
                 (x.Status == TaskExecutionStatus.Cancelled && !x.ParallelGroupId.HasValue))))
            return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                "The completed saga has later or uncertain work; inspect its execution before recovery.");

        var preparationFailure = await PrepareArchivedIngressAsync(instance, cancellationToken);
        if (preparationFailure is not null) return preparationFailure;
        var artifact = await _artifactResolver.ResolveAsync(instance.RuntimeOrchestrationArtifactId.ToString(), cancellationToken);
        if (artifact?.Artifact?.StageDefinitions is null || !HasMatchingParallelDefinitions(stage, artifact.Artifact.StageDefinitions, unresolved))
            return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                "The incomplete parallel tasks do not match the orchestration artifact.");
        var stageDefinition = artifact.Artifact.StageDefinitions.First(x => x.IsEnabled &&
            string.Equals(x.Key, stage.StageKey, StringComparison.OrdinalIgnoreCase));
        var failedParallel = taskExecutions.Where(task => task.StageExecutionId == stage.Id && task.ParallelGroupId.HasValue &&
            task.Status is (TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut) &&
            !FailureIsAllowedToContinue(task, stageDefinition.TaskDefinitions.FirstOrDefault(definition => definition.Key == task.TaskKey))).ToArray();
        if (failedParallel.Length > 0 && !HasMatchingParallelDefinitions(stage, artifact.Artifact.StageDefinitions, failedParallel))
            return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                "The failed parallel tasks do not match the orchestration artifact.");
        var cancelled = unresolved.Where(x => x.Status == TaskExecutionStatus.Cancelled).ToArray();
        var unsafeCancellation = await PrepareCancelledParallelTasksAsync(instance, cancelled, cancellationToken);
        if (unsafeCancellation is not null) return unsafeCancellation;
        await PrepareOtherParallelFailuresAsync(failedParallel, cancellationToken);

        var now = DateTime.UtcNow;
        EnsureProjectedPolicy(instance, artifact.Artifact);
        stage.Status = StageExecutionStatus.Running;
        stage.CompletedOnUtc = null;
        instance.Status = OrchestrationInstanceStatus.Running;
        instance.CompletedOnUtc = null;
        instance.FinalOutcome = string.Empty;
        instance.CurrentStageKey = stage.StageKey;
        instance.CurrentTaskKey = string.Empty;
        instance.LastUpdatedOnUtc = now;
        instance.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        instance.Metadata[ReplayRequestedByMetadataKey] = JsonValue.Create("operator");
        InstanceLifetimePolicy.ResetDeadline(instance, now);
        await using (var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken))
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _stageRepository.Update(stage, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await CreateReplayTransition(instance, stage.Id, null, "StageReplayRequested", "Completed", "Running",
                "Operator resumed a prematurely completed parallel stage.", now, cancellationToken);
            await CreateReplayTransition(instance, null, null, "InstanceReplayRequested", "Completed", "Running",
                "Operator resumed a prematurely completed saga.", now, cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await _sagaEngine.OrchestrateAsync(BuildForwardIntent(instance, payload), cancellationToken);
        if (_reconciliation is not null) await _reconciliation.ReconcileAsync(instance.Id.ToString(), cancellationToken);
        return OrchestrationRecoveryResult.Success(instance.Id.ToString(), "Running", "The incomplete parallel stage was resumed.");
    }

    private Task<OrchestrationRecoveryResult> PrepareCancelledParallelTasksAsync(
        OrchestrationInstance instance, IReadOnlyCollection<TaskExecution> cancelled, CancellationToken cancellationToken)
        => ParallelCancellationRecovery.PrepareAsync(instance, cancelled, _taskRepository, _attemptRepository,
            _dispatchRepository, _storageUnitOfWork, _dispatchCancellationCoordinator, cancellationToken);

    private static bool HasMatchingParallelDefinitions(StageExecution execution, IReadOnlyCollection<StageArtifact> stages,
        IReadOnlyCollection<TaskExecution> tasks)
    {
        var stage = stages?.FirstOrDefault(x => x.IsEnabled &&
            string.Equals(x.Key, execution?.StageKey, StringComparison.OrdinalIgnoreCase));
        return stage?.TaskDefinitions is not null && stage.ParallelGroups is not null && tasks.All(task =>
            task.ParallelGroupId.HasValue && stage.ParallelGroups.Any(group => group.Id == task.ParallelGroupId.Value) &&
            stage.TaskDefinitions.Any(definition => definition.IsEnabled && definition.ParallelGroupId == task.ParallelGroupId &&
                string.Equals(definition.Key, task.TaskKey, StringComparison.Ordinal)));
    }

    private static bool FailureIsAllowedToContinue(TaskExecution task, TaskArtifact definition)
        => definition?.OnErrorPolicy == OnErrorPolicy.Continue ||
            (task.Status == TaskExecutionStatus.TimedOut &&
             definition?.TimeoutPolicy?.TimeoutBehaviorPolicy is WaitTimeoutBehaviorPolicyArtifact wait &&
             wait.OrchestrationAction == OrchestrationActionOnTimeout.Continue);

    private async Task PrepareOtherParallelFailuresAsync(IReadOnlyCollection<TaskExecution> failed, CancellationToken cancellationToken)
    {
        if (failed.Count == 0) return;
        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            foreach (var task in failed)
            {
                task.Metadata[ParallelCancellationRecovery.ResumeFailedMetadataKey] = JsonValue.Create(true);
                await _taskRepository.Update(task, cancellationToken);
            }
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<OrchestrationRecoveryResult> ReplayTaskAsync(
        string instanceId,
        string taskExecutionId,
        string payload = null,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseId(instanceId, out var parsedInstanceId))
        {
            return OrchestrationRecoveryResult.Rejected(instanceId, string.Empty, "Instance id is invalid.");
        }

        if (!TryParseId(taskExecutionId, out var parsedTaskExecutionId))
        {
            return OrchestrationRecoveryResult.Rejected(parsedInstanceId.ToString(), string.Empty, "Task execution id is invalid.");
        }

        OrchestrationRecoveryResult result = null;
        var acquired = await _coordinator.TryExecuteAsync(
            parsedInstanceId,
            async token => result = await ReplayTaskCoreAsync(
                parsedInstanceId,
                parsedTaskExecutionId,
                payload,
                reason,
                token),
            cancellationToken);

        return acquired
            ? result
            : OrchestrationRecoveryResult.Rejected(
                parsedInstanceId.ToString(),
                string.Empty,
                $"Instance '{parsedInstanceId}' is being processed by another runtime node.");
    }

    private async Task<OrchestrationRecoveryResult> ReplayTaskCoreAsync(
        Id instanceId,
        Id taskExecutionId,
        string payload,
        string reason,
        CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetById(instanceId, cancellationToken);
        if (instance is null)
        {
            return OrchestrationRecoveryResult.Rejected(instanceId.ToString(), string.Empty, "Orchestration instance was not found.");
        }

        if (!IsRecoverable(instance.Status))
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                instance.Status.ToString(),
                $"Instance '{instance.Id}' is not in a recoverable status.");
        }

        var task = await _taskRepository.GetById(taskExecutionId, cancellationToken);
        if (task is null || task.OrchestrationInstanceId != instance.Id)
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                instance.Status.ToString(),
                $"Task '{taskExecutionId}' does not belong to instance '{instance.Id}'.");
        }

        if (task.Status is not (TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut))
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                task.Status.ToString(),
                $"Task '{task.TaskKey}' is not failed or timed out and cannot be replayed.");
        }

        var preparationFailure = await PrepareArchivedIngressAsync(instance, cancellationToken);
        if (preparationFailure is not null) return preparationFailure;
        var resolvedArtifact = await _artifactResolver.ResolveAsync(
            instance.RuntimeOrchestrationArtifactId.ToString(),
            cancellationToken);
        EnsureProjectedPolicy(instance, resolvedArtifact.Artifact);
        var stages = (resolvedArtifact.Artifact.StageDefinitions ?? [])
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)
            .ToArray();
        var stageExecutions = await _stageRepository.GetByInstanceId(instance.Id, cancellationToken);

        return await ReplayTaskCoreAsync(
            instance,
            task,
            stageExecutions,
            stages,
            resolvedArtifact.Artifact.MetadataDescriptors,
            payload,
            reason,
            DateTime.UtcNow,
            cancellationToken);
    }

    public async Task<OrchestrationRecoveryResult> ReplayDispatchAsync(
        string instanceId,
        string taskExecutionId,
        string dispatchId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseId(instanceId, out var parsedInstanceId))
        {
            return OrchestrationRecoveryResult.Rejected(instanceId, string.Empty, "Instance id is invalid.");
        }

        if (!TryParseId(taskExecutionId, out var parsedTaskExecutionId))
        {
            return OrchestrationRecoveryResult.Rejected(parsedInstanceId.ToString(), string.Empty, "Task execution id is invalid.");
        }

        if (!TryParseId(dispatchId, out var parsedDispatchId))
        {
            return OrchestrationRecoveryResult.Rejected(parsedInstanceId.ToString(), string.Empty, "Dispatch id is invalid.");
        }

        OrchestrationRecoveryResult result = null;
        var acquired = await _coordinator.TryExecuteAsync(
            parsedInstanceId,
            async token => result = await ReplayDispatchCoreAsync(
                parsedInstanceId,
                parsedTaskExecutionId,
                parsedDispatchId,
                reason,
                token),
            cancellationToken);

        return acquired
            ? result
            : OrchestrationRecoveryResult.Rejected(
                parsedInstanceId.ToString(),
                string.Empty,
                $"Instance '{parsedInstanceId}' is being processed by another runtime node.");
    }

    private async Task<OrchestrationRecoveryResult> ReplayDispatchCoreAsync(
        Id instanceId,
        Id taskExecutionId,
        Id dispatchId,
        string reason,
        CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetById(instanceId, cancellationToken);
        if (instance is null)
        {
            return OrchestrationRecoveryResult.Rejected(instanceId.ToString(), string.Empty, "Orchestration instance was not found.");
        }

        if (!IsRecoverable(instance.Status))
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                instance.Status.ToString(),
                $"Instance '{instance.Id}' is not in a recoverable status.");
        }

        var task = await _taskRepository.GetById(taskExecutionId, cancellationToken);
        if (task is null || task.OrchestrationInstanceId != instance.Id)
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                instance.Status.ToString(),
                $"Task '{taskExecutionId}' does not belong to instance '{instance.Id}'.");
        }

        var dispatch = await _dispatchRepository.GetById(dispatchId, cancellationToken);
        var attempt = dispatch is null
            ? null
            : await _attemptRepository.GetById(dispatch.TaskExecutionAttemptId, cancellationToken);
        if (dispatch is null || attempt is null || attempt.TaskExecutionId != task.Id)
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                task.Status.ToString(),
                $"Dispatch '{dispatchId}' does not belong to task '{task.Id}'.");
        }

        var isPendingReplay = string.Equals(dispatch.DispatchStatus, "ReplayPending", StringComparison.OrdinalIgnoreCase);
        if (!isPendingReplay && !string.Equals(dispatch.DispatchStatus, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                dispatch.DispatchStatus,
                $"Dispatch '{dispatch.Id}' is not failed and cannot be replayed.");
        }

        if (!isPendingReplay && task.Status != TaskExecutionStatus.Failed)
        {
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                task.Status.ToString(),
                $"Task '{task.TaskKey}' must be failed before its durable delivery can be replayed.");
        }

        var preparationFailure = await PrepareArchivedIngressAsync(instance, cancellationToken);
        if (preparationFailure is not null) return preparationFailure;
        var now = DateTime.UtcNow;
        var needsLifetimeProjection = instance.MaxInstanceDurationSeconds is null;
        if (needsLifetimeProjection)
        {
            var resolved = await _artifactResolver.ResolveAsync(instance.RuntimeOrchestrationArtifactId.ToString(), cancellationToken);
            if (resolved?.Artifact is null)
                return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                    "The orchestration lifetime policy could not be resolved for this legacy instance.");
            EnsureProjectedPolicy(instance, resolved.Artifact);
        }
        var requestId = GetOrCreateDispatchReplayRequestId(dispatch);
        if (!isPendingReplay)
        {
            await MarkDispatchReplayPendingAsync(instance, task, attempt, dispatch, reason, now, cancellationToken);
        }
        else if (!instance.Metadata.ContainsKey(ReplayRequestedOnUtcMetadataKey))
        {
            // A replay scheduled before lifetime policies existed needs an anchor.
            instance.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
            InstanceLifetimePolicy.ResetDeadline(instance, now);
            await _instanceRepository.Update(instance, cancellationToken);
        }
        else if (needsLifetimeProjection)
        {
            // Preserve the already-scheduled replay window while projecting a legacy instance.
            InstanceLifetimePolicy.ResetDeadline(instance, InstanceLifetimePolicy.ReadRecoveryAnchor(instance) ?? now);
            await _instanceRepository.Update(instance, cancellationToken);
        }

        TaskDispatchReplayResult providerResult;
        try
        {
            providerResult = await _taskDispatchReplayService.ReplayAsync(
                new TaskDispatchReplayRequest
                {
                    Dispatch = dispatch,
                    RequestId = requestId,
                    RequestedBy = "operator",
                    Reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim()
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            providerResult = TaskDispatchReplayResult.Rejected("ProviderError", exception.Message);
        }

        if (!providerResult.Succeeded)
        {
            await RestoreFailedDispatchReplayAsync(instance, task, attempt, dispatch, providerResult, cancellationToken);
            return OrchestrationRecoveryResult.Rejected(
                instance.Id.ToString(),
                dispatch.DispatchStatus,
                providerResult.Message);
        }

        dispatch.Metadata[DispatchReplayProviderStatusMetadataKey] = JsonValue.Create(providerResult.Status);
        if (!string.IsNullOrWhiteSpace(providerResult.ProviderActionId))
        {
            dispatch.Metadata[DispatchReplayProviderActionIdMetadataKey] = JsonValue.Create(providerResult.ProviderActionId);
        }

        await _dispatchRepository.Update(dispatch, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = task.StageExecutionId,
            TaskExecutionId = task.Id,
            TaskExecutionAttemptId = attempt.Id,
            TransitionType = "TaskDispatchReplayScheduled",
            FromStatus = "ReplayPending",
            ToStatus = "ReplayPending",
            OccurredOnUtc = DateTime.UtcNow,
            Message = providerResult.Message,
            ProducedBy = nameof(DefaultOrchestrationRecoveryService)
        }, cancellationToken);

        return OrchestrationRecoveryResult.Success(
            instance.Id.ToString(),
            OrchestrationInstanceStatus.Running.ToString(),
            providerResult.Message);
    }

    private async Task MarkDispatchReplayPendingAsync(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        string reason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        dispatch.Metadata[DispatchReplayPreviousInstanceStatusMetadataKey] = JsonValue.Create(instance.Status.ToString());
        dispatch.Metadata[DispatchReplayPreviousTaskStatusMetadataKey] = JsonValue.Create(task.Status.ToString());
        dispatch.Metadata[DispatchReplayPreviousAttemptStatusMetadataKey] = JsonValue.Create(attempt.Status.ToString());
        dispatch.Metadata[DispatchReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            dispatch.Metadata[DispatchReplayReasonMetadataKey] = JsonValue.Create(reason.Trim());
        }

        dispatch.DispatchStatus = "ReplayPending";
        task.Status = TaskExecutionStatus.Running;
        task.WaitingSinceUtc = null;
        attempt.Status = TaskExecutionStatus.Running;
        attempt.WaitingSinceUtc = null;
        instance.Status = OrchestrationInstanceStatus.Running;
        instance.WaitingSinceUtc = null;
        instance.IdleSinceUtc = null;
        instance.IdleExpiresOnUtc = null;
        instance.LastUpdatedOnUtc = now;
        instance.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        InstanceLifetimePolicy.ResetDeadline(instance, now);

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using var autoSave = _storageUnitOfWork.DeferAutoSave();
        await _dispatchRepository.Update(dispatch, cancellationToken);
        await _attemptRepository.Update(attempt, cancellationToken);
        await _taskRepository.Update(task, cancellationToken);
        await _instanceRepository.Update(instance, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = task.StageExecutionId,
            TaskExecutionId = task.Id,
            TaskExecutionAttemptId = attempt.Id,
            TransitionType = "TaskDispatchReplayRequested",
            FromStatus = "Failed",
            ToStatus = "ReplayPending",
            OccurredOnUtc = now,
            Message = $"Durable delivery replay requested for task '{task.TaskKey}'.",
            ProducedBy = nameof(DefaultOrchestrationRecoveryService)
        }, cancellationToken);
        await _storageUnitOfWork.SaveChanges(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RestoreFailedDispatchReplayAsync(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        TaskDispatchReplayResult providerResult,
        CancellationToken cancellationToken)
    {
        dispatch.DispatchStatus = "Failed";
        task.Status = ParseTaskStatus(dispatch.Metadata, DispatchReplayPreviousTaskStatusMetadataKey, TaskExecutionStatus.Failed);
        attempt.Status = ParseTaskStatus(dispatch.Metadata, DispatchReplayPreviousAttemptStatusMetadataKey, TaskExecutionStatus.Failed);
        instance.Status = ParseInstanceStatus(
            dispatch.Metadata,
            DispatchReplayPreviousInstanceStatusMetadataKey,
            OrchestrationInstanceStatus.Failed);
        dispatch.Metadata[DispatchReplayProviderStatusMetadataKey] = JsonValue.Create(providerResult.Status);

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using var autoSave = _storageUnitOfWork.DeferAutoSave();
        await _dispatchRepository.Update(dispatch, cancellationToken);
        await _attemptRepository.Update(attempt, cancellationToken);
        await _taskRepository.Update(task, cancellationToken);
        await _instanceRepository.Update(instance, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = task.StageExecutionId,
            TaskExecutionId = task.Id,
            TaskExecutionAttemptId = attempt.Id,
            TransitionType = "TaskDispatchReplayRejected",
            FromStatus = "ReplayPending",
            ToStatus = "Failed",
            OccurredOnUtc = DateTime.UtcNow,
            Message = providerResult.Message,
            ProducedBy = nameof(DefaultOrchestrationRecoveryService)
        }, cancellationToken);
        await _storageUnitOfWork.SaveChanges(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string GetOrCreateDispatchReplayRequestId(TaskDispatch dispatch)
    {
        if (dispatch.Metadata.TryGetValue(DispatchReplayRequestIdMetadataKey, out var existing) &&
            !string.IsNullOrWhiteSpace(existing?.GetValue<string>()))
        {
            return existing.GetValue<string>();
        }

        var requestId = Id.New().ToString();
        dispatch.Metadata[DispatchReplayRequestIdMetadataKey] = JsonValue.Create(requestId);
        return requestId;
    }

    private static TaskExecutionStatus ParseTaskStatus(
        IReadOnlyDictionary<string, JsonNode> metadata,
        string key,
        TaskExecutionStatus fallback)
        => metadata.TryGetValue(key, out var value) &&
           Enum.TryParse<TaskExecutionStatus>(value?.GetValue<string>(), out var status)
            ? status
            : fallback;

    private static OrchestrationInstanceStatus ParseInstanceStatus(
        IReadOnlyDictionary<string, JsonNode> metadata,
        string key,
        OrchestrationInstanceStatus fallback)
        => metadata.TryGetValue(key, out var value) &&
           Enum.TryParse<OrchestrationInstanceStatus>(value?.GetValue<string>(), out var status)
            ? status
            : fallback;

    public async Task<OrchestrationRecoveryResult> AbortAsync(
        string instanceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseId(instanceId, out var parsedInstanceId))
        {
            return OrchestrationRecoveryResult.Rejected(instanceId, string.Empty, "Instance id is invalid.");
        }

        OrchestrationRecoveryResult result = null;
        var acquired = await _coordinator.TryExecuteAsync(
            parsedInstanceId,
            async token => result = await AbortCoreAsync(parsedInstanceId, reason, token),
            cancellationToken);

        return acquired
            ? result
            : OrchestrationRecoveryResult.Rejected(
                parsedInstanceId.ToString(),
                string.Empty,
                $"Instance '{parsedInstanceId}' is being processed by another runtime node.");
    }

    private async Task<OrchestrationRecoveryResult> AbortCoreAsync(
        Id parsedInstanceId,
        string reason,
        CancellationToken cancellationToken)
    {
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
        instance.DispatchCancellationPending = true;
        instance.FailedOnUtc ??= now;
        instance.StoppedOnUtc ??= now;
        instance.WaitingSinceUtc = null;
        instance.IdleSinceUtc = null;
        instance.IdleExpiresOnUtc = null;
        instance.ErrorSummary = abortReason;
        instance.LastUpdatedOnUtc = now;
        instance.Metadata[AbortReasonMetadataKey] = JsonValue.Create(abortReason);
        instance.Metadata[AbortedOnUtcMetadataKey] = JsonValue.Create(now);

        await _instanceRepository.Update(instance, cancellationToken);
        await _dispatchCancellationCoordinator.CancelPendingAsync(instance, abortReason, cancellationToken);
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

    private async Task<OrchestrationRecoveryResult> ReplayTaskCoreAsync(
        OrchestrationInstance instance,
        TaskExecution failedTask,
        IReadOnlyCollection<StageExecution> stageExecutions,
        IReadOnlyCollection<StageArtifact> stages,
        IReadOnlyCollection<MetadataDescriptorArtifact> metadataDescriptors,
        string payload,
        string reason,
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

        if (failedTask.ParallelGroupId.HasValue && stageExecution.Status != StageExecutionStatus.Running)
        {
            if (stageExecution.Status != StageExecutionStatus.Failed)
                return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                    $"Parallel stage '{stageExecution.StageKey}' is {stageExecution.Status}; inspect it before replaying a task.");
            stageExecution.Status = StageExecutionStatus.Running;
            stageExecution.FailedOnUtc = null;
            stageExecution.ErrorSummary = null;
            await _stageRepository.Update(stageExecution, cancellationToken);
            await CreateReplayTransition(instance, stageExecution.Id, null, "StageReplayRequested", "Failed", "Running",
                $"Parallel stage '{stageExecution.StageKey}' resumed for task recovery.", now, cancellationToken);
        }

        var previousTaskStatus = failedTask.Status;
        failedTask.Metadata.Remove("RetrySuppressed");
        failedTask.Metadata["ManualReplay"] = JsonValue.Create(true);
        failedTask.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            failedTask.Metadata[TaskReplayReasonMetadataKey] = JsonValue.Create(reason.Trim());
        }
        instance.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        InstanceLifetimePolicy.ResetDeadline(instance, now);
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
            "TaskReplayRequested",
            previousTaskStatus.ToString(),
            TaskExecutionStatus.Running.ToString(),
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
        instance.IdleSinceUtc = null;
        instance.IdleExpiresOnUtc = null;
        instance.LastUpdatedOnUtc = now;
        instance.Metadata[ReplayRequestedOnUtcMetadataKey] = JsonValue.Create(now);
        InstanceLifetimePolicy.ResetDeadline(instance, now);
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
        string transitionType,
        string fromStatus,
        string toStatus,
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
            TransitionType = transitionType,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            OccurredOnUtc = now,
            Message = message,
            ProducedBy = nameof(DefaultOrchestrationRecoveryService)
        }, cancellationToken);
    }

    private async Task<OrchestrationRecoveryResult> PrepareArchivedIngressAsync(OrchestrationInstance instance, CancellationToken cancellationToken)
    {
        if (_archivedIngressPreparer is null) return null;
        try
        {
            await _archivedIngressPreparer.PrepareAsync(instance.RuntimeOrchestrationArtifactId, cancellationToken);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                $"Recovery could not prepare the orchestration backchannel: {exception.Message}");
        }
    }

    private void EnsureProjectedPolicy(OrchestrationInstance instance, OrchestrationArtifact artifact)
    {
        if (instance.MaxInstanceDurationSeconds is null)
            InstanceLifetimePolicy.Project(instance, artifact, _timeoutOptions, instance.StartedOnUtc);
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
            or OrchestrationInstanceStatus.Idle;

    private static bool IsOperatorRecoverableTerminal(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Failed or OrchestrationInstanceStatus.DeadLettered;

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

    private sealed class PassthroughOrchestrationInstanceCoordinator : IOrchestrationInstanceCoordinator
    {
        public static readonly PassthroughOrchestrationInstanceCoordinator Instance = new();

        public async Task<bool> TryExecuteAsync(
            Id instanceId,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default,
            bool waitForLease = true)
        {
            await operation(cancellationToken);
            return true;
        }
    }
}
