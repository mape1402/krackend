using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
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
    private readonly IOrchestrationInstanceCoordinator _coordinator;
    private readonly IRuntimeAdmissionController _admissionController;
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;

    public DefaultOrchestrationTimeoutProcessor(
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeArtifactResolver artifactResolver,
        ISagaEngine sagaEngine,
        IOrchestrationInstanceCoordinator coordinator,
        IRuntimeAdmissionController admissionController,
        IRuntimeStorageUnitOfWork unitOfWork)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
        _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<int> ProcessDueTimeoutsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.TimeoutProcessing, cancellationToken);

        var waitingTasks = await _taskRepository.GetResponseTimeoutsDue(utcNow, take: 100, cancellationToken);
        var processed = 0;

        foreach (var waitingTask in waitingTasks)
        {
            var result = await ProcessTaskTimeoutAsync(waitingTask.Id, utcNow, cancellationToken);
            if (result.Applied)
            {
                processed++;
            }
        }

        return processed;
    }

    public async Task<TaskResponseTimeoutProcessingResult> ProcessTaskTimeoutAsync(
        Id taskExecutionId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.TimeoutProcessing, cancellationToken);

        TaskResponseTimeoutProcessingResult result = TaskResponseTimeoutProcessingResult.Ignored;
        TaskExecution task;
        try
        {
            task = await _taskRepository.GetById(taskExecutionId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return result;
        }

        var acquired = await _coordinator.TryExecuteAsync(
            task.OrchestrationInstanceId,
            async token => result = await TryProcessTaskCoreAsync(task, utcNow, token),
            cancellationToken);

        return acquired ? result : TaskResponseTimeoutProcessingResult.Ignored;
    }

    private async Task<TaskResponseTimeoutProcessingResult> TryProcessTaskCoreAsync(
        TaskExecution waitingTask,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetById(waitingTask.Id, cancellationToken);
        if (task.Status != TaskExecutionStatus.WaitingResponse || task.WaitingSinceUtc is null)
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        var instance = await _instanceRepository.GetById(task.OrchestrationInstanceId, cancellationToken);
        if (IsTerminal(instance.Status))
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
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
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        var dueOnUtc = task.ResponseDeadlineUtc ?? task.WaitingSinceUtc.Value.Add(timeoutPolicy.Timeout.Value);
        if (dueOnUtc > utcNow)
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        var waitGraceResult = await TryApplyWaitGraceAsync(instance, task, timeoutPolicy, utcNow, cancellationToken);
        if (waitGraceResult == WaitGraceResult.Waiting)
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        if (waitGraceResult == WaitGraceResult.Applied)
        {
            return new TaskResponseTimeoutProcessingResult(true, task.ResponseDeadlineUtc);
        }

        var attempts = await _attemptRepository.GetByTaskExecutionId(task.Id, cancellationToken);
        var attempt = attempts
            .OrderByDescending(x => x.AttemptNumber)
            .FirstOrDefault(x => x.Status == TaskExecutionStatus.WaitingResponse);
        if (attempt is null)
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        var dispatch = attempt.DispatchId.HasValue
            ? await _dispatchRepository.TryGetById(attempt.DispatchId.Value, cancellationToken)
            : null;
        if (dispatch?.SentOnUtc is null)
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        var errorCode = ResolveTimeoutErrorCode(timeoutPolicy);
        var errorMessage = $"Task '{task.TaskKey}' timed out after waiting {timeoutPolicy.Timeout.Value}.";

        ApplyTimeoutState(instance, task, attempt, dispatch, timeoutPolicy, errorCode, errorMessage, utcNow);

        await PersistTimeoutAsync(
            instance,
            task,
            attempt,
            dispatch,
            errorMessage,
            utcNow,
            cancellationToken);

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

        return new TaskResponseTimeoutProcessingResult(true);
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
        task.ResponseDeadlineUtc = graceExpiresOnUtc;
        instance.LastUpdatedOnUtc = utcNow;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
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
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return WaitGraceResult.Applied;
    }

    private async Task PersistTimeoutAsync(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
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
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
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
        task.ResponseDeadlineUtc = null;
        task.TimeoutActionId = null;
        task.TimeoutActionLane = null;
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

        // A timeout belongs to this task attempt. The aggregate instance stays Running while
        // DecisionControl evaluates retries, continuation, compensation, or its idle window.
        instance.Status = OrchestrationInstanceStatus.Running;
        instance.WaitingSinceUtc = null;
        instance.IdleSinceUtc = null;
        instance.IdleExpiresOnUtc = null;
        instance.FailedOnUtc = null;
        instance.ErrorSummary = errorMessage;
        instance.LastUpdatedOnUtc = utcNow;
    }

    private static bool IsTerminal(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Completed
            or OrchestrationInstanceStatus.CompletedWithErrors
            or OrchestrationInstanceStatus.Stopped
            or OrchestrationInstanceStatus.Compensating
            or OrchestrationInstanceStatus.Compensated
            or OrchestrationInstanceStatus.DeadLettered
            or OrchestrationInstanceStatus.Aborted
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
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
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
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
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
