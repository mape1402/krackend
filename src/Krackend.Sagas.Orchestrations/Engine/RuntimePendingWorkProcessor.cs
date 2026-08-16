using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Default pending work processor.
/// </summary>
public sealed class RuntimePendingWorkProcessor : IRuntimePendingWorkProcessor
{
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly ICompensationExecutionRepository _compensationRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRuntimeTaskDispatcherResolver _taskDispatcherResolver;
    private readonly IRuntimeTimeoutPolicyEvaluator _timeoutPolicyEvaluator;
    private readonly IRuntimeErrorPolicyResolver _errorPolicyResolver;
    private readonly IRuntimeCompensationExecutor _compensationExecutor;
    private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;
    private readonly IRuntimeEngine _runtimeEngine;
    private readonly IRuntimeCompensationPlanBuilder _compensationPlanBuilder = new RuntimeCompensationPlanBuilder();

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimePendingWorkProcessor"/> class.
    /// </summary>
    public RuntimePendingWorkProcessor(
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        ICompensationExecutionRepository compensationRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        IRuntimeArtifactRepository artifactRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeTaskDispatcherResolver taskDispatcherResolver,
        IRuntimeTimeoutPolicyEvaluator timeoutPolicyEvaluator,
        IRuntimeErrorPolicyResolver errorPolicyResolver,
        IRuntimeCompensationExecutor compensationExecutor,
        IRuntimeReactiveEventPublisher reactiveEventPublisher,
        IRuntimeEngine runtimeEngine)
    {
        _taskRepository = taskRepository;
        _attemptRepository = attemptRepository;
        _dispatchRepository = dispatchRepository;
        _compensationRepository = compensationRepository;
        _instanceRepository = instanceRepository;
        _stageRepository = stageRepository;
        _artifactRepository = artifactRepository;
        _transitionRepository = transitionRepository;
        _taskDispatcherResolver = taskDispatcherResolver;
        _timeoutPolicyEvaluator = timeoutPolicyEvaluator;
        _errorPolicyResolver = errorPolicyResolver;
        _compensationExecutor = compensationExecutor;
        _reactiveEventPublisher = reactiveEventPublisher;
        _runtimeEngine = runtimeEngine;
    }

    /// <inheritdoc/>
    public async Task<RuntimePendingWorkResult> ProcessDueWork(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var waitingTasks = await _taskRepository.GetWaitingResponseOlderThan(nowUtc, cancellationToken);
        var waitingAttempts = await _attemptRepository.GetWaitingResponseOlderThan(nowUtc, cancellationToken);
        var scheduledDispatches = await _dispatchRepository.GetScheduledOlderThan(nowUtc, cancellationToken);
        var compensations = await _compensationRepository.GetPending(cancellationToken);

        var resumedTaskIds = new HashSet<Id>();
        foreach (var task in waitingTasks)
        {
            if (await TryResumeTerminalAttempt(task, cancellationToken))
                resumedTaskIds.Add(task.Id);
        }

        var unresolvedWaitingTasks = waitingTasks
            .Where(x => !resumedTaskIds.Contains(x.Id))
            .ToArray();

        foreach (var task in unresolvedWaitingTasks)
        {
            await TryApplyTimeout(task, nowUtc, CancellationToken.None);
        }

        foreach (var compensation in compensations)
        {
            await _compensationExecutor.Execute(compensation, CancellationToken.None);
        }

        var items = unresolvedWaitingTasks
            .Select(x => new RuntimePendingWorkItem(
                RuntimePendingWorkTypes.WaitingTaskTimeout,
                x.Id,
                x.OrchestrationInstanceId,
                x.Id,
                x.WaitingSinceUtc,
                x.Status.ToString()))
            .Concat(waitingAttempts.Select(x => new RuntimePendingWorkItem(
                RuntimePendingWorkTypes.WaitingAttemptTimeout,
                x.Id,
                null,
                x.TaskExecutionId,
                x.WaitingSinceUtc,
                x.Status.ToString())))
            .Concat(scheduledDispatches.Select(x => new RuntimePendingWorkItem(
                RuntimePendingWorkTypes.ScheduledDispatch,
                x.Id,
                null,
                x.TaskExecutionAttemptId,
                x.ScheduledOnUtc,
                x.DispatchStatus)))
            .Concat(compensations.Select(x => new RuntimePendingWorkItem(
                RuntimePendingWorkTypes.PendingCompensation,
                x.Id,
                x.OrchestrationInstanceId,
                x.SourceTaskExecutionId,
                x.StartedOnUtc,
                x.Status)))
            .OrderBy(x => x.DueOnUtc ?? DateTime.MaxValue)
            .ToArray();

        return new RuntimePendingWorkResult(nowUtc, items);
    }

    private async Task<bool> TryResumeTerminalAttempt(TaskExecution taskExecution, CancellationToken cancellationToken)
    {
        if (taskExecution.Status != TaskExecutionStatus.WaitingResponse)
            return false;

        var attempts = await _attemptRepository.GetByTaskExecutionId(taskExecution.Id, cancellationToken);
        var terminalAttempt = attempts
            .Where(x => x.Status is TaskExecutionStatus.Completed or TaskExecutionStatus.Failed &&
                        x.DispatchId != default)
            .OrderByDescending(x => x.AttemptNumber)
            .FirstOrDefault();

        if (terminalAttempt is null)
            return false;

        var instance = await _instanceRepository.GetById(taskExecution.OrchestrationInstanceId, cancellationToken);
        if (instance.Status is not (OrchestrationInstanceStatus.Waiting or OrchestrationInstanceStatus.Running))
            return false;

        var result = await _runtimeEngine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = instance.Id.ToString(),
            TaskExecutionId = taskExecution.Id.ToString(),
            DispatchId = terminalAttempt.DispatchId.ToString(),
            CorrelationId = taskExecution.CorrelationId,
            Payload = terminalAttempt.ResponsePayload?.DeepClone()
        }, cancellationToken);

        return result.Succeeded;
    }

    private async Task<bool> TryApplyTimeout(TaskExecution taskExecution, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetById(taskExecution.OrchestrationInstanceId, cancellationToken);
        if (instance.Status is not (OrchestrationInstanceStatus.Waiting or OrchestrationInstanceStatus.Running) ||
            taskExecution.Status != TaskExecutionStatus.WaitingResponse)
            return false;

        var stageExecution = await _stageRepository.GetById(taskExecution.StageExecutionId, cancellationToken);
        var artifact = await _artifactRepository.GetById(instance.RuntimeOrchestrationArtifactId, cancellationToken);
        var document = RuntimeArtifactDocument.Parse(artifact.ArtifactPayload, artifact.Version.ToString());
        var taskDocument = FindTaskDocument(document, stageExecution.StageKey, taskExecution.TaskKey);
        var timeoutPolicy = _timeoutPolicyEvaluator.Evaluate(taskDocument.TimeoutPolicy);
        if (!timeoutPolicy.IsConfigured)
            return false;

        if (IsTimeoutPolicyAlreadyApplied(taskExecution))
            return false;

        var attempts = await _attemptRepository.GetByTaskExecutionId(taskExecution.Id, cancellationToken);
        var attempt = attempts
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse)
            .OrderByDescending(x => x.AttemptNumber)
            .FirstOrDefault();

        if (string.Equals(timeoutPolicy.Behavior, nameof(TimeoutBehavior.Reconcile), StringComparison.OrdinalIgnoreCase))
            return await ApplyReconcileTimeout(document, instance, stageExecution, taskExecution, taskDocument, attempt, timeoutPolicy, nowUtc, cancellationToken);

        if (string.Equals(timeoutPolicy.Behavior, nameof(TimeoutBehavior.Wait), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(timeoutPolicy.OrchestrationAction, nameof(OrchestrationActionOnTimeout.Block), StringComparison.OrdinalIgnoreCase))
        {
            if (attempt is not null)
            {
                attempt.TimedOutOnUtc = nowUtc;
                attempt.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
                attempt.Metadata["timeoutAction"] = timeoutPolicy.OrchestrationAction;
                await _attemptRepository.Update(attempt, cancellationToken);
                await WriteTransition(RuntimeTransition.ForAttempt(
                    instance,
                    "TaskTimedOut",
                    TaskExecutionStatus.WaitingResponse,
                    attempt.Status,
                    stageExecution,
                    taskExecution,
                    attempt), cancellationToken);
            }

            taskExecution.TimedOutOnUtc = nowUtc;
            taskExecution.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
            taskExecution.Metadata["timeoutAction"] = timeoutPolicy.OrchestrationAction;
            taskExecution.Metadata["timeoutPolicyApplied"] = "Blocked";
            await _taskRepository.Update(taskExecution, cancellationToken);
            await WriteTransition(RuntimeTransition.ForTask(
                instance,
                "TaskTimeoutPolicyApplied",
                TaskExecutionStatus.WaitingResponse,
                taskExecution.Status,
                stageExecution,
                taskExecution), cancellationToken);
            return true;
        }

        if (!string.Equals(timeoutPolicy.Behavior, nameof(TimeoutBehavior.Fail), StringComparison.OrdinalIgnoreCase))
            return false;

        if (attempt is not null)
        {
            attempt.Status = TaskExecutionStatus.TimedOut;
            attempt.TimedOutOnUtc = nowUtc;
            attempt.ErrorCode = string.IsNullOrWhiteSpace(timeoutPolicy.ErrorCode) ? "TaskTimeout" : timeoutPolicy.ErrorCode;
            attempt.ErrorMessage = $"Task '{taskExecution.TaskKey}' timed out after {timeoutPolicy.Timeout}.";
            await _attemptRepository.Update(attempt, cancellationToken);
            await WriteTransition(RuntimeTransition.ForAttempt(
                instance,
                "TaskTimedOut",
                TaskExecutionStatus.WaitingResponse,
                attempt.Status,
                stageExecution,
                taskExecution,
                attempt), cancellationToken);
        }

        taskExecution.Status = TaskExecutionStatus.Failed;
        taskExecution.TimedOutOnUtc = nowUtc;
        taskExecution.WaitingSinceUtc = null;
        taskExecution.FailedOnUtc = nowUtc;
        taskExecution.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
        taskExecution.Metadata["timeoutErrorCode"] = string.IsNullOrWhiteSpace(timeoutPolicy.ErrorCode) ? "TaskTimeout" : timeoutPolicy.ErrorCode;
        await _taskRepository.Update(taskExecution, cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(
            instance,
            "TaskTimeoutPolicyApplied",
            TaskExecutionStatus.WaitingResponse,
            taskExecution.Status,
            stageExecution,
            taskExecution), cancellationToken);

        await ApplyTimeoutErrorPolicy(document, instance, stageExecution, taskExecution, cancellationToken);
        return true;
    }

    private async Task<bool> ApplyUnsupportedReconcileTimeout(
        OrchestrationInstance instance,
        StageExecution stageExecution,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        RuntimeTimeoutPolicy timeoutPolicy,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (attempt is not null)
        {
            attempt.TimedOutOnUtc = nowUtc;
            attempt.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
            attempt.Metadata["timeoutAction"] = timeoutPolicy.OrchestrationAction;
            attempt.Metadata["reconciliationStatus"] = "Unsupported";
            attempt.Metadata["reconciliationReason"] = "Reconcile timeout behavior has no executable destination or transport in the promoted artifact.";
            await _attemptRepository.Update(attempt, cancellationToken);
            await WriteTransition(RuntimeTransition.ForAttempt(
                instance,
                "TaskTimedOut",
                TaskExecutionStatus.WaitingResponse,
                attempt.Status,
                stageExecution,
                taskExecution,
                attempt), cancellationToken);
        }

        taskExecution.TimedOutOnUtc = nowUtc;
        taskExecution.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
        taskExecution.Metadata["timeoutAction"] = timeoutPolicy.OrchestrationAction;
        taskExecution.Metadata["timeoutPolicyApplied"] = "ReconciliationUnsupported";
        taskExecution.Metadata["reconciliationStatus"] = "Unsupported";
        taskExecution.Metadata["reconciliationReason"] = "Reconcile timeout behavior has no executable destination or transport in the promoted artifact.";
        await _taskRepository.Update(taskExecution, cancellationToken);

        instance.Metadata["reconciliationStatus"] = "Unsupported";
        instance.Metadata["reconciliationTaskKey"] = taskExecution.TaskKey;
        instance.Metadata["reconciliationReason"] = "Reconcile timeout behavior has no executable destination or transport in the promoted artifact.";
        instance.LastUpdatedOnUtc = nowUtc;
        await _instanceRepository.Update(instance, cancellationToken);

        var payload = new Dictionary<string, JsonNode>
        {
            ["timeoutBehavior"] = timeoutPolicy.Behavior,
            ["timeoutAction"] = timeoutPolicy.OrchestrationAction,
            ["reconciliationStatus"] = "Unsupported",
            ["reason"] = "Reconcile timeout behavior has no executable destination or transport in the promoted artifact."
        }.ToJsonObject();

        await WriteTransition(RuntimeTransition.ForTaskPayload(
            instance,
            "TaskReconciliationUnsupported",
            TaskExecutionStatus.WaitingResponse,
            taskExecution.Status,
            stageExecution,
            taskExecution,
            payload), cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(
            instance,
            "TaskTimeoutPolicyApplied",
            TaskExecutionStatus.WaitingResponse,
            taskExecution.Status,
            stageExecution,
            taskExecution), cancellationToken);
        return true;
    }

    private async Task<bool> ApplyReconcileTimeout(
        RuntimeArtifactDocument document,
        OrchestrationInstance instance,
        StageExecution stageExecution,
        TaskExecution taskExecution,
        RuntimeTaskDocument taskDocument,
        TaskExecutionAttempt attempt,
        RuntimeTimeoutPolicy timeoutPolicy,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var dispatcher = _taskDispatcherResolver.Resolve(taskDocument.Kind);
        if (dispatcher is null)
            return await ApplyUnsupportedReconcileTimeout(instance, stageExecution, taskExecution, attempt, timeoutPolicy, nowUtc, cancellationToken);

        var attempts = await _attemptRepository.GetByTaskExecutionId(taskExecution.Id, cancellationToken);
        var lastAttemptNumber = Math.Max(taskExecution.LastAttemptNumber, attempts.Select(x => x.AttemptNumber).DefaultIfEmpty(0).Max());
        var retryPolicy = timeoutPolicy.ReconcileRetryPolicy ?? new RuntimeRetryPolicy();
        if (lastAttemptNumber >= retryPolicy.MaxAttempts)
            return await ExhaustReconcileTimeout(document, instance, stageExecution, taskExecution, attempt, timeoutPolicy, nowUtc, cancellationToken);

        if (attempt is not null)
        {
            attempt.Status = TaskExecutionStatus.TimedOut;
            attempt.TimedOutOnUtc = nowUtc;
            attempt.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
            attempt.Metadata["timeoutAction"] = timeoutPolicy.OrchestrationAction;
            attempt.Metadata["reconciliationStatus"] = "Retrying";
            await _attemptRepository.Update(attempt, cancellationToken);
            await WriteTransition(RuntimeTransition.ForAttempt(
                instance,
                "TaskTimedOut",
                TaskExecutionStatus.WaitingResponse,
                attempt.Status,
                stageExecution,
                taskExecution,
                attempt), cancellationToken);
        }

        var retryAttemptNumber = lastAttemptNumber + 1;
        var started = nowUtc;
        var retryAttempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = taskExecution.Id,
            AttemptNumber = retryAttemptNumber,
            Status = TaskExecutionStatus.Running,
            StartedOnUtc = started,
            RequestPayload = (attempt?.RequestPayload ?? instance.SnapshotPayload)?.DeepClone(),
            Metadata = new Dictionary<string, JsonNode>
            {
                ["reconciliationStatus"] = "Redispatching",
                ["reconciliationAttempt"] = retryAttemptNumber,
                ["timeoutBehavior"] = timeoutPolicy.Behavior,
                ["timeoutAction"] = timeoutPolicy.OrchestrationAction
            }
        };

        taskExecution.Status = TaskExecutionStatus.Running;
        taskExecution.LastAttemptNumber = retryAttemptNumber;
        taskExecution.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
        taskExecution.Metadata["timeoutAction"] = timeoutPolicy.OrchestrationAction;
        taskExecution.Metadata["timeoutPolicyApplied"] = "ReconciliationRedispatched";
        taskExecution.Metadata["reconciliationStatus"] = "Redispatching";
        taskExecution.Metadata["reconciliationAttempt"] = retryAttemptNumber;
        await _taskRepository.Update(taskExecution, cancellationToken);
        await _attemptRepository.Create(retryAttempt, cancellationToken);

        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = retryAttempt.Id,
            DispatchType = taskDocument.Kind ?? string.Empty,
            Destination = taskDocument.Destination ?? string.Empty,
            RequestPayload = retryAttempt.RequestPayload?.DeepClone(),
            DispatchStatus = "Pending",
            CommandId = Id.New().ToString(),
            CorrelationId = taskExecution.CorrelationId
        };
        await _dispatchRepository.Create(dispatch, cancellationToken);

        await WriteTransition(RuntimeTransition.ForAttemptPayload(
            instance,
            "TaskReconciliationRedispatching",
            TaskExecutionStatus.TimedOut,
            TaskExecutionStatus.Running,
            stageExecution,
            taskExecution,
            retryAttempt,
            retryAttempt.RequestPayload), cancellationToken);

        var result = await dispatcher.Dispatch(CreateDispatchRequest(instance, document.Version, stageExecution, taskExecution, taskDocument, retryAttempt, dispatch, started), cancellationToken);
        if (!result.Succeeded)
        {
            dispatch.DispatchStatus = "Failed";
            dispatch.FailedOnUtc = nowUtc;
            dispatch.FailureReason = result.FailureReason;
            await _dispatchRepository.Update(dispatch, cancellationToken);

            retryAttempt.Status = TaskExecutionStatus.Failed;
            retryAttempt.FailedOnUtc = nowUtc;
            retryAttempt.ErrorCode = "MessagingReconciliationDispatchFailed";
            retryAttempt.ErrorMessage = result.FailureReason;
            retryAttempt.DispatchId = dispatch.Id;
            await _attemptRepository.Update(retryAttempt, cancellationToken);

            taskExecution.Status = TaskExecutionStatus.Failed;
            taskExecution.FailedOnUtc = nowUtc;
            taskExecution.WaitingSinceUtc = null;
            taskExecution.Metadata["reconciliationStatus"] = "DispatchFailed";
            taskExecution.Metadata["failureReason"] = result.FailureReason;
            await _taskRepository.Update(taskExecution, cancellationToken);
            await WriteTransition(RuntimeTransition.ForAttempt(
                instance,
                "TaskReconciliationDispatchFailed",
                TaskExecutionStatus.Running,
                TaskExecutionStatus.Failed,
                stageExecution,
                taskExecution,
                retryAttempt), cancellationToken);
            await ApplyTimeoutErrorPolicy(document, instance, stageExecution, taskExecution, cancellationToken);
            return true;
        }

        dispatch.DispatchStatus = string.IsNullOrWhiteSpace(result.Status) ? "Accepted" : result.Status;
        dispatch.Metadata["externalReference"] = result.ExternalReference ?? string.Empty;
        if (string.Equals(dispatch.DispatchStatus, "Scheduled", StringComparison.OrdinalIgnoreCase))
            dispatch.ScheduledOnUtc ??= DateTime.UtcNow;

        if (IsTransportSentStatus(dispatch.DispatchStatus))
        {
            dispatch.SentOnUtc = DateTime.UtcNow;
            dispatch.AcknowledgedOnUtc = dispatch.SentOnUtc;
        }

        await _dispatchRepository.Update(dispatch, cancellationToken);

        retryAttempt.DispatchId = dispatch.Id;
        retryAttempt.Status = TaskExecutionStatus.WaitingResponse;
        retryAttempt.CompletedOnUtc = DateTime.UtcNow;
        retryAttempt.WaitingSinceUtc = DateTime.UtcNow;
        retryAttempt.Metadata["reconciliationStatus"] = "WaitingResponse";
        await _attemptRepository.Update(retryAttempt, cancellationToken);

        taskExecution.Status = TaskExecutionStatus.WaitingResponse;
        taskExecution.WaitingSinceUtc = retryAttempt.WaitingSinceUtc;
        taskExecution.Metadata["reconciliationStatus"] = "WaitingResponse";
        await _taskRepository.Update(taskExecution, cancellationToken);

        instance.Status = OrchestrationInstanceStatus.Waiting;
        instance.WaitingSinceUtc = retryAttempt.WaitingSinceUtc;
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        instance.Metadata["reconciliationStatus"] = "Redispatched";
        instance.Metadata["reconciliationTaskKey"] = taskExecution.TaskKey;
        instance.Metadata["reconciliationAttempt"] = retryAttemptNumber;
        await _instanceRepository.Update(instance, cancellationToken);

        await WriteTransition(RuntimeTransition.ForAttempt(
            instance,
            "TaskReconciliationRedispatched",
            TaskExecutionStatus.Running,
            TaskExecutionStatus.WaitingResponse,
            stageExecution,
            taskExecution,
            retryAttempt), cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(
            instance,
            "TaskTimeoutPolicyApplied",
            TaskExecutionStatus.WaitingResponse,
            taskExecution.Status,
            stageExecution,
            taskExecution), cancellationToken);
        return true;
    }

    private async Task<bool> ExhaustReconcileTimeout(
        RuntimeArtifactDocument document,
        OrchestrationInstance instance,
        StageExecution stageExecution,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        RuntimeTimeoutPolicy timeoutPolicy,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (attempt is not null)
        {
            attempt.Status = TaskExecutionStatus.TimedOut;
            attempt.TimedOutOnUtc = nowUtc;
            attempt.ErrorCode = "TaskReconciliationExhausted";
            attempt.ErrorMessage = $"Task '{taskExecution.TaskKey}' exhausted reconciliation attempts.";
            attempt.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
            attempt.Metadata["timeoutAction"] = timeoutPolicy.OrchestrationAction;
            attempt.Metadata["reconciliationStatus"] = "Exhausted";
            await _attemptRepository.Update(attempt, cancellationToken);
            await WriteTransition(RuntimeTransition.ForAttempt(
                instance,
                "TaskTimedOut",
                TaskExecutionStatus.WaitingResponse,
                attempt.Status,
                stageExecution,
                taskExecution,
                attempt), cancellationToken);
        }

        taskExecution.Status = TaskExecutionStatus.Failed;
        taskExecution.TimedOutOnUtc = nowUtc;
        taskExecution.FailedOnUtc = nowUtc;
        taskExecution.WaitingSinceUtc = null;
        taskExecution.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
        taskExecution.Metadata["timeoutAction"] = timeoutPolicy.OrchestrationAction;
        taskExecution.Metadata["timeoutPolicyApplied"] = "ReconciliationExhausted";
        taskExecution.Metadata["reconciliationStatus"] = "Exhausted";
        taskExecution.Metadata["errorCode"] = "TaskReconciliationExhausted";
        taskExecution.Metadata["errorMessage"] = $"Task '{taskExecution.TaskKey}' exhausted reconciliation attempts.";
        await _taskRepository.Update(taskExecution, cancellationToken);

        await WriteTransition(RuntimeTransition.ForTask(
            instance,
            "TaskReconciliationExhausted",
            TaskExecutionStatus.WaitingResponse,
            taskExecution.Status,
            stageExecution,
            taskExecution), cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(
            instance,
            "TaskTimeoutPolicyApplied",
            TaskExecutionStatus.WaitingResponse,
            taskExecution.Status,
            stageExecution,
            taskExecution), cancellationToken);

        await ApplyTimeoutErrorPolicy(document, instance, stageExecution, taskExecution, cancellationToken);
        return true;
    }

    private async Task ApplyTimeoutErrorPolicy(
        RuntimeArtifactDocument document,
        OrchestrationInstance instance,
        StageExecution stageExecution,
        TaskExecution taskExecution,
        CancellationToken cancellationToken)
    {
        var decision = _errorPolicyResolver.Resolve(taskExecution.OnErrorPolicy);
        taskExecution.Metadata["errorPolicyApplied"] = decision.Policy.ToString();
        taskExecution.Metadata["errorPolicyAction"] = decision.Action.ToString();

        if (decision.Action == RuntimeErrorPolicyAction.Continue)
        {
            taskExecution.Status = TaskExecutionStatus.CompletedWithErrors;
            taskExecution.CompletedOnUtc = DateTime.UtcNow;
            await _taskRepository.Update(taskExecution, cancellationToken);
            await WriteTransition(RuntimeTransition.ForTask(instance, "TaskErrorPolicyApplied", TaskExecutionStatus.Failed, taskExecution.Status, stageExecution, taskExecution), cancellationToken);
            return;
        }

        await _taskRepository.Update(taskExecution, cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(instance, "TaskErrorPolicyApplied", TaskExecutionStatus.Failed, taskExecution.Status, stageExecution, taskExecution), cancellationToken);

        stageExecution.Status = StageExecutionStatus.Failed;
        stageExecution.FailedOnUtc = DateTime.UtcNow;
        stageExecution.ErrorSummary = taskExecution.ErrorSummary();
        await _stageRepository.Update(stageExecution, cancellationToken);

        if (decision.Action == RuntimeErrorPolicyAction.StartCompensation)
        {
            instance.Status = OrchestrationInstanceStatus.Compensating;
            instance.CompensationStartedOnUtc = DateTime.UtcNow;
            instance.WaitingSinceUtc = null;
            instance.LastUpdatedOnUtc = DateTime.UtcNow;
            instance.ErrorSummary = taskExecution.ErrorSummary();
            await _instanceRepository.Update(instance, cancellationToken);
            await WriteTransition(RuntimeTransition.ForStage(instance, "InstanceCompensating", OrchestrationInstanceStatus.Waiting, instance.Status, stageExecution), cancellationToken);

            var completedTasks = await _taskRepository.GetByInstanceId(instance.Id, cancellationToken);
            var existingCompensations = await _compensationRepository.GetByInstanceId(instance.Id, cancellationToken);
            var existingSourceTaskIds = existingCompensations.Select(x => x.SourceTaskExecutionId).ToHashSet();
            var plan = _compensationPlanBuilder.Build(document, completedTasks)
                .Where(x => !existingSourceTaskIds.Contains(x.SourceTaskExecutionId))
                .ToArray();

            foreach (var item in plan)
            {
                await _compensationRepository.Create(new CompensationExecution
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    SourceTaskExecutionId = item.SourceTaskExecutionId,
                    CompensationTaskKey = item.CompensationTaskKey,
                    Status = "Pending",
                    RequestPayload = item.RequestPayload?.DeepClone(),
                    Metadata = item.Metadata.ToDictionary(x => x.Key, x => x.Value?.DeepClone())
                }, cancellationToken);

                await WriteTransition(RuntimeTransition.ForInstancePayload(
                    instance,
                    "CompensationScheduled",
                    OrchestrationInstanceStatus.Compensating,
                    OrchestrationInstanceStatus.Compensating,
                    item.Metadata.ToJsonObject()), cancellationToken);
            }

            instance.Metadata["compensationPlanCount"] = plan.Length;
            await _instanceRepository.Update(instance, cancellationToken);
            return;
        }

        instance.Status = OrchestrationInstanceStatus.Failed;
        instance.FailedOnUtc = DateTime.UtcNow;
        instance.WaitingSinceUtc = null;
        instance.ErrorSummary = stageExecution.ErrorSummary;
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(instance, "StageFailed", StageExecutionStatus.Running, stageExecution.Status, stageExecution, taskExecution), cancellationToken);
        await WriteTransition(RuntimeTransition.ForStage(instance, "InstanceFailed", OrchestrationInstanceStatus.Waiting, instance.Status, stageExecution), cancellationToken);
    }

    private static RuntimeTaskDocument FindTaskDocument(RuntimeArtifactDocument document, string stageKey, string taskKey)
    {
        var stage = document.Stages.FirstOrDefault(x => string.Equals(x.Key, stageKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Stage '{stageKey}' was not found in runtime artifact.");
        return stage.Tasks.FirstOrDefault(x => string.Equals(x.Key, taskKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Task '{taskKey}' was not found in stage '{stageKey}'.");
    }

    private static bool IsTimeoutPolicyAlreadyApplied(TaskExecution taskExecution)
    {
        if (taskExecution.Metadata.TryGetValue("timeoutPolicyApplied", out var applied) &&
            applied is JsonValue value &&
            value.TryGetValue<string>(out var text) &&
            !string.IsNullOrWhiteSpace(text))
            return !string.Equals(text, "ReconciliationRedispatched", StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private static RuntimeTaskDispatchRequest CreateDispatchRequest(
        OrchestrationInstance instance,
        string orchestrationVersion,
        StageExecution stageExecution,
        TaskExecution taskExecution,
        RuntimeTaskDocument taskDocument,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        DateTime startedOnUtc)
    {
        return new RuntimeTaskDispatchRequest
        {
            CommandId = dispatch.CommandId,
            CorrelationId = taskExecution.CorrelationId,
            TaskKind = taskDocument.Kind,
            DispatchType = taskDocument.DispatchType,
            Destination = taskDocument.Destination ?? string.Empty,
            MessageVersion = string.IsNullOrWhiteSpace(taskDocument.MessageVersion) ? "1.0.0" : taskDocument.MessageVersion,
            Payload = dispatch.RequestPayload?.DeepClone(),
            OrchestrationDefinitionKey = instance.OrchestrationDefinitionKey,
            OrchestrationVersion = orchestrationVersion,
            OrchestrationInstanceId = instance.Id.ToString(),
            TaskExecutionId = taskExecution.Id.ToString(),
            DispatchId = dispatch.Id.ToString(),
            EnvironmentKey = instance.EnvironmentKey,
            StageKey = stageExecution.StageKey,
            TaskKey = taskExecution.TaskKey,
            CurrentStatus = taskExecution.Status.ToString(),
            Attempt = attempt.AttemptNumber,
            StartedOnUtc = startedOnUtc,
            UpdatedOnUtc = DateTime.UtcNow
        };
    }

    private static bool IsTransportSentStatus(string status)
        => string.Equals(status, "Dispatched", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase);

    private async Task WriteTransition(RuntimeTransition transition, CancellationToken cancellationToken)
    {
        var executionTransition = new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = transition.Instance.Id,
            StageExecutionId = transition.StageExecution?.Id,
            TaskExecutionId = transition.TaskExecution?.Id,
            TaskExecutionAttemptId = transition.Attempt?.Id,
            TransitionType = transition.Type,
            FromStatus = transition.FromStatus,
            ToStatus = transition.ToStatus,
            OccurredOnUtc = DateTime.UtcNow,
            Message = transition.Type,
            Payload = transition.Payload?.DeepClone(),
            ProducedBy = "Krackend.Sagas.Orchestrations.Engine"
        };

        await _transitionRepository.Create(executionTransition, cancellationToken);
        try
        {
            await _reactiveEventPublisher.Publish(new RuntimeReactiveEvent
            {
                Id = executionTransition.Id,
                EventName = ResolveReactiveEventName(transition.Type),
                TransitionType = transition.Type,
                EnvironmentKey = transition.Instance.EnvironmentKey,
                OrchestrationDefinitionKey = transition.Instance.OrchestrationDefinitionKey,
                OrchestrationInstanceId = transition.Instance.Id,
                CorrelationId = transition.Instance.CorrelationId,
                ExecutionKey = transition.Instance.ExecutionKey,
                StageExecutionId = transition.StageExecution?.Id,
                StageKey = transition.StageExecution?.StageKey,
                TaskExecutionId = transition.TaskExecution?.Id,
                TaskKey = transition.TaskExecution?.TaskKey,
                TaskExecutionAttemptId = transition.Attempt?.Id,
                FromStatus = executionTransition.FromStatus,
                ToStatus = executionTransition.ToStatus,
                InstanceStatus = transition.Instance.Status.ToString(),
                OccurredOnUtc = executionTransition.OccurredOnUtc,
                Message = executionTransition.Message,
                Payload = executionTransition.Payload?.DeepClone(),
                ProducedBy = executionTransition.ProducedBy
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Pending work state is persisted; live observer outages must not fail recovery processing.
        }
    }

    private static string ResolveReactiveEventName(string transitionType)
        => transitionType switch
        {
            "InstanceCompensating" => RuntimeReactiveEventNames.OrchestrationCompensating,
            "InstanceFailed" => RuntimeReactiveEventNames.OrchestrationFailed,
            "StageFailed" => RuntimeReactiveEventNames.StageFailed,
            "TaskTimedOut" => RuntimeReactiveEventNames.TaskTimedOut,
            "TaskTimeoutPolicyApplied" => RuntimeReactiveEventNames.TaskTimeoutPolicyApplied,
            "TaskReconciliationUnsupported" => RuntimeReactiveEventNames.TaskReconciliationUnsupported,
            "TaskReconciliationRedispatching" => RuntimeReactiveEventNames.TaskTimeoutPolicyApplied,
            "TaskReconciliationRedispatched" => RuntimeReactiveEventNames.TaskTimeoutPolicyApplied,
            "TaskReconciliationDispatchFailed" => RuntimeReactiveEventNames.TaskFailed,
            "TaskReconciliationExhausted" => RuntimeReactiveEventNames.TaskFailed,
            "TaskErrorPolicyApplied" => RuntimeReactiveEventNames.TaskErrorPolicyApplied,
            "CompensationScheduled" => RuntimeReactiveEventNames.CompensationScheduled,
            "CompensationStarted" => RuntimeReactiveEventNames.CompensationStarted,
            "CompensationDispatched" => RuntimeReactiveEventNames.CompensationDispatched,
            "CompensationCompleted" => RuntimeReactiveEventNames.CompensationCompleted,
            "CompensationFailed" => RuntimeReactiveEventNames.CompensationFailed,
            "InstanceCompensated" => RuntimeReactiveEventNames.OrchestrationCompensated,
            _ => RuntimeReactiveEventNames.TransitionRecorded
        };
}
