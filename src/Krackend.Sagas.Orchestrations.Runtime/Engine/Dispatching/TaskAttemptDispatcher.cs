namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;

internal sealed class TaskAttemptDispatcher : ITaskAttemptDispatcher
{
    private readonly ITaskRuntimeAdapterRegistry _adapterRegistry;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRemoteCommandDispatcher _dispatcher;
    private readonly ITaskDispatchRequestPayloadPreparer _requestPayloadPreparer;
    private readonly IOrchestrationPayloadState _payloadState;
    private readonly IOrchestrationPropagationMetadataStore _propagationMetadataStore;

    public TaskAttemptDispatcher(
        ITaskRuntimeAdapterRegistry adapterRegistry,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IExecutionTransitionRepository transitionRepository,
        IRemoteCommandDispatcher dispatcher,
        ITaskDispatchRequestPayloadPreparer requestPayloadPreparer,
        IOrchestrationPayloadState payloadState,
        IOrchestrationPropagationMetadataStore propagationMetadataStore = null)
    {
        _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _requestPayloadPreparer = requestPayloadPreparer ?? throw new ArgumentNullException(nameof(requestPayloadPreparer));
        _payloadState = payloadState ?? throw new ArgumentNullException(nameof(payloadState));
        _propagationMetadataStore = propagationMetadataStore ?? new DefaultOrchestrationPropagationMetadataStore();
    }

    public async Task DispatchAsync(TaskAttemptDispatchRequest request, CancellationToken cancellationToken = default)
    {
        if (!_adapterRegistry.TryGet(request.Task.Kind, out var adapter))
        {
            await MarkDispatchConfigurationFailedAsync(
                request,
                "TaskRuntimeAdapterNotConfigured",
                $"No runtime task adapter is configured for task kind '{request.Task.Kind}'.",
                "TaskDispatchAdapterMissing",
                cancellationToken);
            return;
        }

        var validation = adapter.ValidateTask(request.Task, request.StageKey);
        if (!validation.Succeeded)
        {
            await MarkDispatchConfigurationFailedAsync(
                request,
                string.IsNullOrWhiteSpace(validation.ErrorCode) ? "TaskRuntimeAdapterValidationFailed" : validation.ErrorCode,
                string.IsNullOrWhiteSpace(validation.ErrorMessage) ? $"Task '{request.Task.Key}' is not compatible with the runtime adapter for '{request.Task.Kind}'." : validation.ErrorMessage,
                "TaskDispatchConfigurationFailed",
                cancellationToken);
            return;
        }

        var now = request.NowUtc == default ? DateTime.UtcNow : request.NowUtc;
        var taskExecution = request.Kind == TaskAttemptDispatchKind.Initial
            ? CreateInitialTaskExecution(request, now)
            : request.TaskExecution;
        var attemptNumber = request.Kind == TaskAttemptDispatchKind.Initial
            ? 1
            : taskExecution.LastAttemptNumber + 1;
        var scheduledOnUtc = request.ScheduledOnUtc ?? new DateTimeOffset(now, TimeSpan.Zero);
        var hasDeferredDispatch = request.Kind == TaskAttemptDispatchKind.Retry &&
            scheduledOnUtc > DateTimeOffset.UtcNow;
        var requestPayload = ParsePayload(request.Payload);
        var previousStatus = taskExecution.Status;
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = taskExecution.Id,
            AttemptNumber = attemptNumber,
            Status = TaskExecutionStatus.Running,
            StartedOnUtc = now,
            RequestPayload = requestPayload?.DeepClone()
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = request.Task.DispatchType.ToString(),
            Destination = adapter.GetDestination(request.Task),
            RequestPayload = requestPayload?.DeepClone(),
            DispatchStatus = "Scheduled",
            CommandId = Id.New().ToString(),
            CorrelationId = ResolveCorrelationId(request.Instance, taskExecution),
            ScheduledOnUtc = scheduledOnUtc.UtcDateTime
        };
        attempt.DispatchId = dispatch.Id;

        PrepareRunningState(request, taskExecution, attemptNumber, now, requestPayload);

        if (request.Kind == TaskAttemptDispatchKind.Initial)
        {
            await _taskRepository.Create(taskExecution, cancellationToken);
        }
        else
        {
            await _taskRepository.Update(taskExecution, cancellationToken);
        }

        await _attemptRepository.Create(attempt, cancellationToken);
        await _dispatchRepository.Create(dispatch, cancellationToken);
        await _instanceRepository.Update(request.Instance, cancellationToken);

        try
        {
            var preparation = await _requestPayloadPreparer.PrepareAsync(
                new TaskDispatchRequestPayloadPreparationRequest
                {
                    Instance = request.Instance,
                    StageKey = request.StageKey,
                    Task = request.Task,
                    Configuration = request.Task.Configuration,
                    MetadataDescriptors = request.MetadataDescriptors,
                    Payload = request.Payload
                },
                cancellationToken);

            requestPayload = preparation.Payload;
            attempt.RequestPayload = requestPayload?.DeepClone();
            dispatch.RequestPayload = requestPayload?.DeepClone();
            request.Instance.SnapshotPayload = _payloadState.ApplyTaskRequestPayload(
                request.Instance,
                request.StageKey,
                request.Task.Key,
                requestPayload);
        }
        catch (TaskDispatchPreparationException exception)
        {
            await MarkPreparationFailedAsync(
                request,
                taskExecution,
                attempt,
                dispatch,
                exception,
                cancellationToken);
            return;
        }

        var queuedOnUtc = DateTime.UtcNow;
        dispatch.DispatchStatus = hasDeferredDispatch ? "Scheduled" : "Enqueued";
        request.Instance.LastUpdatedOnUtc = queuedOnUtc;
        await _attemptRepository.Update(attempt, cancellationToken);
        await _dispatchRepository.Update(dispatch, cancellationToken);
        await _taskRepository.Update(taskExecution, cancellationToken);
        await _instanceRepository.Update(request.Instance, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = request.Instance.Id,
            StageExecutionId = request.StageExecutionId,
            TaskExecutionId = taskExecution.Id,
            TaskExecutionAttemptId = attempt.Id,
            TransitionType = ResolveQueuedTransitionType(request.Kind, hasDeferredDispatch),
            FromStatus = ResolveQueuedFromStatus(request.Kind, previousStatus),
            ToStatus = dispatch.DispatchStatus,
            OccurredOnUtc = queuedOnUtc,
            Message = BuildQueuedMessage(request, attemptNumber, scheduledOnUtc, dispatch.DispatchStatus),
            Payload = requestPayload?.DeepClone(),
            ProducedBy = nameof(TaskAttemptDispatcher)
        }, cancellationToken);

        await QueueAsync(
            request,
            adapter,
            taskExecution,
            attempt,
            dispatch,
            hasDeferredDispatch ? scheduledOnUtc : null,
            requestPayload?.ToJsonString(),
            cancellationToken);
    }

    private async Task QueueAsync(
        TaskAttemptDispatchRequest request,
        ITaskRuntimeAdapter adapter,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        DateTimeOffset? scheduledOnUtc,
        string commandPayload,
        CancellationToken cancellationToken)
    {
        try
        {
            var descriptor = await adapter.BuildCommandAsync(new TaskRuntimeCommandRequest
            {
                Instance = request.Instance,
                StageExecutionId = request.StageExecutionId,
                StageKey = request.StageKey,
                Task = request.Task,
                TaskExecution = taskExecution,
                Attempt = attempt,
                Dispatch = dispatch,
                Payload = commandPayload
            }, cancellationToken);
            var propagationMetadata = _propagationMetadataStore.Load(request.Instance);
            var messageMetadata = new OrchestrationMessageMetadata
            {
                SagaId = GetSagaId(request.Instance),
                OrchestrationInstanceId = request.Instance.Id.ToString(),
                CurrentStage = request.StageKey,
                CurrentTasks = [request.Task.Key],
                CorrelationId = taskExecution.CorrelationId,
                TaskExecutionId = taskExecution.Id.ToString(),
                DispatchId = dispatch.Id.ToString(),
                Attempt = attempt.AttemptNumber,
                ReplyAddress = descriptor.ReplyAddress
            };

            dispatch.Metadata = RemoteCommandMetadataBuilder.Build(messageMetadata, propagationMetadata);
            await _dispatchRepository.Update(dispatch, cancellationToken);

            await _dispatcher.DispatchAsync(new RemoteCommand
            {
                Payload = commandPayload,
                RemoteCommandTransport = descriptor.Transport,
                SettingsPayload = descriptor.SettingsPayload,
                OrchestrationInstanceId = request.Instance.Id.ToString(),
                StageExecutionId = request.StageExecutionId.ToString(),
                TaskExecutionId = taskExecution.Id.ToString(),
                TaskExecutionAttemptId = attempt.Id.ToString(),
                DispatchId = dispatch.Id.ToString(),
                StageKey = request.StageKey,
                TaskKey = request.Task.Key,
                AwaitResponse = request.Task.DispatchType != TaskDispatchType.FireAndForget,
                ScheduledOnUtc = scheduledOnUtc,
                PropagationMetadata = propagationMetadata,
                MessageMetadata = messageMetadata
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            await MarkQueueFailedAsync(request, taskExecution, attempt, dispatch, exception, cancellationToken);
        }
    }

    private async Task MarkDispatchConfigurationFailedAsync(
        TaskAttemptDispatchRequest request,
        string errorCode,
        string message,
        string initialTransitionType,
        CancellationToken cancellationToken)
    {
        var failedOnUtc = DateTime.UtcNow;
        if (request.Kind == TaskAttemptDispatchKind.Retry)
        {
            var taskExecution = request.TaskExecution;
            var previousStatus = taskExecution.Status;
            taskExecution.Status = TaskExecutionStatus.Failed;
            taskExecution.FailedOnUtc = failedOnUtc;
            taskExecution.Metadata["RetrySuppressed"] = JsonValue.Create(true);
            taskExecution.Metadata["RetryConfigurationErrorCode"] = JsonValue.Create(errorCode);
            taskExecution.Metadata["RetryConfigurationError"] = JsonValue.Create(message);
            request.Instance.Status = request.Task.OnErrorPolicy == OnErrorPolicy.Continue
                ? OrchestrationInstanceStatus.Running
                : OrchestrationInstanceStatus.Failed;
            request.Instance.FailedOnUtc = request.Task.OnErrorPolicy == OnErrorPolicy.Continue ? null : failedOnUtc;
            request.Instance.ErrorSummary = message;
            request.Instance.LastUpdatedOnUtc = failedOnUtc;

            await _taskRepository.Update(taskExecution, cancellationToken);
            await _instanceRepository.Update(request.Instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = request.Instance.Id,
                StageExecutionId = request.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TransitionType = "TaskRetryConfigurationFailed",
                FromStatus = previousStatus.ToString(),
                ToStatus = taskExecution.Status.ToString(),
                OccurredOnUtc = failedOnUtc,
                Message = message,
                ProducedBy = nameof(TaskAttemptDispatcher)
            }, cancellationToken);
            return;
        }

        var failedTask = CreateInitialTaskExecution(request, failedOnUtc);
        failedTask.Status = TaskExecutionStatus.Failed;
        failedTask.AwaitResponse = false;
        failedTask.FailedOnUtc = failedOnUtc;
        failedTask.LastAttemptNumber = 0;
        failedTask.Metadata["ExecutionErrorCode"] = JsonValue.Create(errorCode);
        failedTask.Metadata["ExecutionErrorMessage"] = JsonValue.Create(message);
        failedTask.Metadata["RetrySuppressed"] = JsonValue.Create(true);
        request.Instance.Status = OrchestrationInstanceStatus.Failed;
        request.Instance.CurrentStageKey = request.StageKey;
        request.Instance.CurrentTaskKey = request.Task.Key;
        request.Instance.FailedOnUtc = failedOnUtc;
        request.Instance.ErrorSummary = message;
        request.Instance.LastUpdatedOnUtc = failedOnUtc;
        request.Instance.WaitingSinceUtc = null;

        await _taskRepository.Create(failedTask, cancellationToken);
        await _instanceRepository.Update(request.Instance, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = request.Instance.Id,
            StageExecutionId = request.StageExecutionId,
            TaskExecutionId = failedTask.Id,
            TransitionType = initialTransitionType,
            FromStatus = TaskExecutionStatus.Pending.ToString(),
            ToStatus = TaskExecutionStatus.Failed.ToString(),
            OccurredOnUtc = failedOnUtc,
            Message = message,
            ProducedBy = nameof(TaskAttemptDispatcher)
        }, cancellationToken);
    }

    private async Task MarkPreparationFailedAsync(
        TaskAttemptDispatchRequest request,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        TaskDispatchPreparationException exception,
        CancellationToken cancellationToken)
    {
        var failedOnUtc = DateTime.UtcNow;
        dispatch.DispatchStatus = "Failed";
        dispatch.FailedOnUtc = failedOnUtc;
        dispatch.FailureReason = exception.Message;
        taskExecution.Status = TaskExecutionStatus.Failed;
        taskExecution.FailedOnUtc = failedOnUtc;
        attempt.Status = TaskExecutionStatus.Failed;
        attempt.FailedOnUtc = failedOnUtc;
        attempt.ErrorCode = exception.ErrorCode;
        attempt.ErrorMessage = exception.Message;
        attempt.Metadata["PreparationErrorCode"] = JsonValue.Create(exception.ErrorCode);

        foreach (var diagnostic in exception.Diagnostics)
        {
            attempt.Metadata[$"Preparation.{diagnostic.Key}"] = diagnostic.Value?.DeepClone();
        }

        request.Instance.Status = request.Task.OnErrorPolicy == OnErrorPolicy.Continue
            ? OrchestrationInstanceStatus.Running
            : OrchestrationInstanceStatus.Failed;
        request.Instance.FailedOnUtc = request.Task.OnErrorPolicy == OnErrorPolicy.Continue ? null : failedOnUtc;
        request.Instance.ErrorSummary = exception.Message;
        request.Instance.LastUpdatedOnUtc = failedOnUtc;

        await _dispatchRepository.Update(dispatch, cancellationToken);
        await _taskRepository.Update(taskExecution, cancellationToken);
        await _attemptRepository.Update(attempt, cancellationToken);
        await _instanceRepository.Update(request.Instance, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = request.Instance.Id,
            StageExecutionId = request.StageExecutionId,
            TaskExecutionId = taskExecution.Id,
            TaskExecutionAttemptId = attempt.Id,
            TransitionType = request.Kind == TaskAttemptDispatchKind.Initial
                ? "TaskDispatchPreparationFailed"
                : "TaskRetryPreparationFailed",
            FromStatus = TaskExecutionStatus.Running.ToString(),
            ToStatus = TaskExecutionStatus.Failed.ToString(),
            OccurredOnUtc = failedOnUtc,
            Message = exception.Message,
            ProducedBy = nameof(TaskAttemptDispatcher)
        }, cancellationToken);
    }

    private async Task MarkQueueFailedAsync(
        TaskAttemptDispatchRequest request,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var failedOnUtc = DateTime.UtcNow;
        dispatch.DispatchStatus = "Failed";
        dispatch.FailedOnUtc = failedOnUtc;
        dispatch.FailureReason = exception.Message;
        taskExecution.Status = TaskExecutionStatus.Failed;
        taskExecution.FailedOnUtc = failedOnUtc;
        attempt.Status = TaskExecutionStatus.Failed;
        attempt.FailedOnUtc = failedOnUtc;
        attempt.ErrorCode = "CommandDispatchFailed";
        attempt.ErrorMessage = exception.Message;
        request.Instance.Status = request.Task.OnErrorPolicy == OnErrorPolicy.Continue
            ? OrchestrationInstanceStatus.Running
            : OrchestrationInstanceStatus.Failed;
        request.Instance.FailedOnUtc = request.Task.OnErrorPolicy == OnErrorPolicy.Continue ? null : failedOnUtc;
        request.Instance.ErrorSummary = exception.Message;
        request.Instance.LastUpdatedOnUtc = failedOnUtc;

        await _dispatchRepository.Update(dispatch, cancellationToken);
        await _attemptRepository.Update(attempt, cancellationToken);
        await _taskRepository.Update(taskExecution, cancellationToken);
        await _instanceRepository.Update(request.Instance, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = request.Instance.Id,
            StageExecutionId = request.StageExecutionId,
            TaskExecutionId = taskExecution.Id,
            TaskExecutionAttemptId = attempt.Id,
            TransitionType = request.Kind == TaskAttemptDispatchKind.Initial
                ? "TaskDispatchQueueFailed"
                : "TaskRetryQueueFailed",
            FromStatus = request.Kind == TaskAttemptDispatchKind.Initial
                ? TaskExecutionStatus.Running.ToString()
                : "Enqueued",
            ToStatus = TaskExecutionStatus.Failed.ToString(),
            OccurredOnUtc = failedOnUtc,
            Message = exception.Message,
            ProducedBy = nameof(TaskAttemptDispatcher)
        }, cancellationToken);
    }

    private static TaskExecution CreateInitialTaskExecution(TaskAttemptDispatchRequest request, DateTime now)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = request.Instance.Id,
            StageExecutionId = request.StageExecutionId,
            TaskKey = request.Task.Key,
            TaskKind = request.Task.Kind,
            ExecutionMode = request.Task.ExecutionMode,
            ParallelGroupId = request.Task.ParallelGroupId,
            Status = TaskExecutionStatus.Running,
            WasSkipped = false,
            SkipReason = string.Empty,
            ExecutionConditionResult = true,
            OnErrorPolicy = request.Task.OnErrorPolicy,
            AwaitResponse = request.Task.DispatchType != TaskDispatchType.FireAndForget,
            StartedOnUtc = now,
            LastAttemptNumber = 1,
            CorrelationId = string.IsNullOrWhiteSpace(request.Instance.CorrelationId)
                ? Id.New().ToString()
                : request.Instance.CorrelationId
        };

    private void PrepareRunningState(
        TaskAttemptDispatchRequest request,
        TaskExecution taskExecution,
        int attemptNumber,
        DateTime now,
        JsonNode requestPayload)
    {
        taskExecution.Status = TaskExecutionStatus.Running;
        taskExecution.LastAttemptNumber = attemptNumber;
        taskExecution.FailedOnUtc = null;
        taskExecution.WaitingSinceUtc = null;
        taskExecution.Metadata["Timeout"] = JsonValue.Create(request.Task.TimeoutPolicy?.Timeout.ToString() ?? string.Empty);
        request.Instance.Status = OrchestrationInstanceStatus.Running;
        request.Instance.CurrentStageKey = request.StageKey;
        request.Instance.CurrentTaskKey = request.Task.Key;
        request.Instance.FailedOnUtc = null;
        request.Instance.ErrorSummary = null;
        request.Instance.LastUpdatedOnUtc = now;
        request.Instance.SnapshotPayload = _payloadState.ApplyTaskRequestPayload(
            request.Instance,
            request.StageKey,
            request.Task.Key,
            requestPayload);
    }

    private static string ResolveCorrelationId(OrchestrationInstance instance, TaskExecution taskExecution)
        => string.IsNullOrWhiteSpace(taskExecution.CorrelationId)
            ? string.IsNullOrWhiteSpace(instance.CorrelationId) ? Id.New().ToString() : instance.CorrelationId
            : taskExecution.CorrelationId;

    private static string ResolveQueuedTransitionType(TaskAttemptDispatchKind kind, bool hasDeferredDispatch)
        => kind == TaskAttemptDispatchKind.Initial
            ? "TaskDispatchEnqueued"
            : hasDeferredDispatch ? "TaskRetryScheduled" : "TaskRetryEnqueued";

    private static string ResolveQueuedFromStatus(TaskAttemptDispatchKind kind, TaskExecutionStatus previousStatus)
        => kind == TaskAttemptDispatchKind.Initial
            ? TaskExecutionStatus.Pending.ToString()
            : previousStatus.ToString();

    private static string BuildQueuedMessage(
        TaskAttemptDispatchRequest request,
        int attemptNumber,
        DateTimeOffset scheduledOnUtc,
        string dispatchStatus)
    {
        if (request.Kind == TaskAttemptDispatchKind.Initial)
        {
            return $"Task '{request.Task.Key}' dispatch enqueued.";
        }

        return dispatchStatus == "Scheduled"
            ? $"Retry attempt {attemptNumber} scheduled for task '{request.Task.Key}' at {scheduledOnUtc:O}."
            : $"Retry attempt {attemptNumber} enqueued for task '{request.Task.Key}'.";
    }

    private static JsonNode ParsePayload(string payload)
        => string.IsNullOrWhiteSpace(payload) ? null : JsonNode.Parse(payload);

    private static string GetSagaId(OrchestrationInstance instance)
        => string.IsNullOrWhiteSpace(instance.SagaId)
            ? instance.Id.ToString()
            : instance.SagaId;
}
