using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

internal sealed class DefaultOrchestrationTimeoutProcessor : IOrchestrationTimeoutProcessor
{
    private const string DefaultTimeoutErrorCode = "TaskTimedOut";
    private const string TimeoutObservedOnUtcMetadataKey = "TimeoutObservedOnUtc";
    private const string TimeoutGraceExpiresOnUtcMetadataKey = "TimeoutGraceExpiresOnUtc";

    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRuntimeArtifactResolver _artifactResolver;
    private readonly ISagaEngine _sagaEngine;

    public DefaultOrchestrationTimeoutProcessor(
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeArtifactResolver artifactResolver,
        ISagaEngine sagaEngine)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
        _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
    }

    public async Task<int> ProcessDueTimeoutsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var waitingTasks = await _taskRepository.GetWaitingResponseOlderThan(utcNow, cancellationToken);
        var processed = 0;

        foreach (var waitingTask in waitingTasks)
        {
            if (await TryProcessTaskAsync(waitingTask, utcNow, cancellationToken))
            {
                processed++;
            }
        }

        return processed;
    }

    private async Task<bool> TryProcessTaskAsync(
        TaskExecution waitingTask,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetById(waitingTask.Id, cancellationToken);
        if (task.Status != TaskExecutionStatus.WaitingResponse || task.WaitingSinceUtc is null)
        {
            return false;
        }

        var instance = await _instanceRepository.GetById(task.OrchestrationInstanceId, cancellationToken);
        if (IsTerminal(instance.Status))
        {
            return false;
        }

        var stage = await _stageRepository.GetById(task.StageExecutionId, cancellationToken);
        var resolvedArtifact = await _artifactResolver.ResolveAsync(instance.RuntimeOrchestrationArtifactId.ToString(), cancellationToken);
        var taskArtifact = resolvedArtifact.Artifact.StageDefinitions
            .FirstOrDefault(x => string.Equals(x.Key, stage.StageKey, StringComparison.OrdinalIgnoreCase))?
            .TaskDefinitions
            .FirstOrDefault(x => string.Equals(x.Key, task.TaskKey, StringComparison.OrdinalIgnoreCase));
        var timeoutPolicy = taskArtifact?.TimeoutPolicy;
        if (timeoutPolicy is null)
        {
            return false;
        }

        var dueOnUtc = task.WaitingSinceUtc.Value.Add(timeoutPolicy.Timeout.Value);
        if (dueOnUtc > utcNow)
        {
            return false;
        }

        var waitGraceResult = await TryApplyWaitGraceAsync(instance, task, timeoutPolicy, utcNow, cancellationToken);
        if (waitGraceResult == WaitGraceResult.Waiting)
        {
            return false;
        }

        if (waitGraceResult == WaitGraceResult.Applied)
        {
            return true;
        }

        var attempts = await _attemptRepository.GetByTaskExecutionId(task.Id, cancellationToken);
        var attempt = attempts
            .OrderByDescending(x => x.AttemptNumber)
            .FirstOrDefault(x => x.Status == TaskExecutionStatus.WaitingResponse);
        if (attempt is null)
        {
            return false;
        }

        var dispatch = attempt.DispatchId.HasValue
            ? await _dispatchRepository.TryGetById(attempt.DispatchId.Value, cancellationToken)
            : null;
        var errorCode = ResolveTimeoutErrorCode(timeoutPolicy);
        var errorMessage = $"Task '{task.TaskKey}' timed out after waiting {timeoutPolicy.Timeout.Value}.";

        ApplyTimeoutState(instance, task, attempt, dispatch, timeoutPolicy, errorCode, errorMessage, utcNow);

        if (dispatch is not null)
        {
            await _dispatchRepository.Update(dispatch, cancellationToken);
        }

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
            TransitionType = "TaskTimedOut",
            FromStatus = TaskExecutionStatus.WaitingResponse.ToString(),
            ToStatus = TaskExecutionStatus.TimedOut.ToString(),
            OccurredOnUtc = utcNow,
            Message = errorMessage,
            ProducedBy = nameof(DefaultOrchestrationTimeoutProcessor)
        }, cancellationToken);

        await _sagaEngine.OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                SagaId = GetSagaId(instance),
                OrchestrationInstanceId = instance.Id.ToString(),
                CurrentStage = stage.StageKey,
                CurrentTasks = [task.TaskKey],
                CorrelationId = instance.CorrelationId,
                TaskExecutionId = task.Id.ToString(),
                DispatchId = dispatch?.Id.ToString(),
                Attempt = attempt.AttemptNumber
            },
            ExecutionResultMetadata = new OrchestrationExecutionResultMetadata
            {
                Succeeded = false,
                Status = "TimedOut",
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                ErrorType = "Timeout",
                CompletedOnUtc = utcNow
            },
            Payload = instance.SnapshotPayload?.DeepClone()
        }, cancellationToken);

        return true;
    }

    private async Task<WaitGraceResult> TryApplyWaitGraceAsync(
        OrchestrationInstance instance,
        TaskExecution task,
        TimeoutPolicyArtifact timeoutPolicy,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (timeoutPolicy.TimeoutBehaviorPolicy is not WaitTimeoutBehaviorPolicyArtifact wait)
        {
            return WaitGraceResult.NotApplicable;
        }

        if (TryGetDateTime(task.Metadata, TimeoutGraceExpiresOnUtcMetadataKey, out var graceExpiresOnUtc))
        {
            return graceExpiresOnUtc > utcNow ? WaitGraceResult.Waiting : WaitGraceResult.NotApplicable;
        }

        graceExpiresOnUtc = utcNow.Add(wait.WaitingTime.Value);
        task.Metadata[TimeoutObservedOnUtcMetadataKey] = JsonValue.Create(utcNow);
        task.Metadata[TimeoutGraceExpiresOnUtcMetadataKey] = JsonValue.Create(graceExpiresOnUtc);
        instance.LastUpdatedOnUtc = utcNow;

        await _taskRepository.Update(task, cancellationToken);
        await _instanceRepository.Update(instance, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = task.StageExecutionId,
            TaskExecutionId = task.Id,
            TransitionType = "TaskTimeoutPolicyApplied",
            FromStatus = TaskExecutionStatus.WaitingResponse.ToString(),
            ToStatus = TaskExecutionStatus.WaitingResponse.ToString(),
            OccurredOnUtc = utcNow,
            Message = $"Wait timeout policy applied for task '{task.TaskKey}' until {graceExpiresOnUtc:O}.",
            ProducedBy = nameof(DefaultOrchestrationTimeoutProcessor)
        }, cancellationToken);

        return WaitGraceResult.Applied;
    }

    private static void ApplyTimeoutState(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        TimeoutPolicyArtifact timeoutPolicy,
        string errorCode,
        string errorMessage,
        DateTime utcNow)
    {
        task.Status = TaskExecutionStatus.TimedOut;
        task.WaitingSinceUtc = null;
        task.FailedOnUtc = utcNow;
        task.TimedOutOnUtc = utcNow;
        task.Metadata["TimeoutBehavior"] = JsonValue.Create(timeoutPolicy.TimeoutBehavior.ToString());
        task.Metadata["TimeoutErrorCode"] = JsonValue.Create(errorCode);

        attempt.Status = TaskExecutionStatus.TimedOut;
        attempt.WaitingSinceUtc = null;
        attempt.FailedOnUtc = utcNow;
        attempt.TimedOutOnUtc = utcNow;
        attempt.ErrorCode = errorCode;
        attempt.ErrorMessage = errorMessage;
        attempt.Metadata["ExecutionSucceeded"] = JsonValue.Create(false);
        attempt.Metadata["ExecutionStatus"] = JsonValue.Create("TimedOut");
        attempt.Metadata["ExecutionErrorCode"] = JsonValue.Create(errorCode);
        attempt.Metadata["ExecutionErrorMessage"] = JsonValue.Create(errorMessage);
        attempt.Metadata["ExecutionErrorType"] = JsonValue.Create("Timeout");
        attempt.Metadata["ExecutionCompletedOnUtc"] = JsonValue.Create(utcNow);

        if (dispatch is not null)
        {
            dispatch.DispatchStatus = "TimedOut";
            dispatch.FailedOnUtc = utcNow;
            dispatch.FailureReason = errorMessage;
        }

        instance.Status = task.OnErrorPolicy == OnErrorPolicy.Continue || IsContinueTimeout(timeoutPolicy)
            ? OrchestrationInstanceStatus.Running
            : OrchestrationInstanceStatus.Failed;
        instance.WaitingSinceUtc = null;
        instance.FailedOnUtc = instance.Status == OrchestrationInstanceStatus.Failed ? utcNow : null;
        instance.ErrorSummary = errorMessage;
        instance.LastUpdatedOnUtc = utcNow;
    }

    private static bool IsTerminal(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Completed
            or OrchestrationInstanceStatus.CompletedWithErrors
            or OrchestrationInstanceStatus.Stopped
            or OrchestrationInstanceStatus.Compensating
            or OrchestrationInstanceStatus.Compensated
            or OrchestrationInstanceStatus.Failed;

    private static bool IsContinueTimeout(TimeoutPolicyArtifact timeoutPolicy)
        => timeoutPolicy.TimeoutBehaviorPolicy is WaitTimeoutBehaviorPolicyArtifact wait &&
            wait.OrchestrationAction == OrchestrationActionOnTimeout.Continue;

    private static string ResolveTimeoutErrorCode(TimeoutPolicyArtifact timeoutPolicy)
        => timeoutPolicy.TimeoutBehaviorPolicy is FailTimeoutBehaviorPolicyArtifact fail &&
            !string.IsNullOrWhiteSpace(fail.ErrorCode)
            ? fail.ErrorCode
            : DefaultTimeoutErrorCode;

    private static bool TryGetDateTime(
        IReadOnlyDictionary<string, JsonNode> metadata,
        string key,
        out DateTime value)
    {
        value = default;
        if (metadata is null ||
            !metadata.TryGetValue(key, out var node) ||
            node is null)
        {
            return false;
        }

        try
        {
            value = node.GetValue<DateTime>();
            return true;
        }
        catch (InvalidOperationException)
        {
        }
        catch (FormatException)
        {
        }

        try
        {
            return DateTime.TryParse(
                node.GetValue<string>(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out value);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string GetSagaId(OrchestrationInstance instance)
        => string.IsNullOrWhiteSpace(instance.SagaId)
            ? instance.Id.ToString()
            : instance.SagaId;

    private enum WaitGraceResult
    {
        NotApplicable,
        Waiting,
        Applied
    }
}
