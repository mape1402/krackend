namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Execution;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;

internal sealed class TaskAttemptDispatcher : ITaskAttemptDispatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private const string DeliveryProviderKeyMetadataKey = "Runtime.Delivery.ProviderKey";
    private const string DeliveryCommandMetadataKey = "Runtime.Delivery.Command";

    private readonly ITaskRuntimeAdapterRegistry _adapterRegistry;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly ITaskDispatchRequestPayloadPreparer _requestPayloadPreparer;
    private readonly IOrchestrationPayloadState _payloadState;
    private readonly IOrchestrationPropagationMetadataStore _propagationMetadataStore;
    private readonly IExecutionPolicyResolver _executionPolicyResolver;
    private readonly IRuntimeStorageUnitOfWork _storageUnitOfWork;

    public TaskAttemptDispatcher(
        ITaskRuntimeAdapterRegistry adapterRegistry,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IExecutionTransitionRepository transitionRepository,
        ITaskDispatchRequestPayloadPreparer requestPayloadPreparer,
        IOrchestrationPayloadState payloadState,
        IExecutionPolicyResolver executionPolicyResolver,
        IRuntimeStorageUnitOfWork storageUnitOfWork,
        IOrchestrationPropagationMetadataStore propagationMetadataStore = null)
    {
        _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _requestPayloadPreparer = requestPayloadPreparer ?? throw new ArgumentNullException(nameof(requestPayloadPreparer));
        _payloadState = payloadState ?? throw new ArgumentNullException(nameof(payloadState));
        _executionPolicyResolver = executionPolicyResolver ?? throw new ArgumentNullException(nameof(executionPolicyResolver));
        _storageUnitOfWork = storageUnitOfWork ?? throw new ArgumentNullException(nameof(storageUnitOfWork));
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

        var executionPolicy = _executionPolicyResolver.Resolve(new ExecutionPolicyResolutionRequest
        {
            StageKey = request.StageKey,
            Task = request.Task,
            OrchestrationPolicy = request.OrchestrationExecutionPolicy,
            StagePolicy = request.StageExecutionPolicy
        });

        if (!executionPolicy.Succeeded)
        {
            await MarkDispatchConfigurationFailedAsync(
                request,
                string.IsNullOrWhiteSpace(executionPolicy.ErrorCode) ? "ExecutionPolicyResolutionFailed" : executionPolicy.ErrorCode,
                string.IsNullOrWhiteSpace(executionPolicy.ErrorMessage) ? $"Execution policy could not be resolved for task '{request.Task.Key}'." : executionPolicy.ErrorMessage,
                "TaskExecutionPolicyFailed",
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
        ApplyExecutionPolicySnapshot(taskExecution, attempt, dispatch, executionPolicy.ResolvedPolicy);

        PrepareRunningState(request, taskExecution, attemptNumber, now, requestPayload);

        PreparedDispatch preparedDispatch;
        try
        {
            preparedDispatch = await PrepareDispatchAsync(
                request,
                adapter,
                executionPolicy,
                taskExecution,
                attempt,
                dispatch,
                hasDeferredDispatch ? scheduledOnUtc : null,
                cancellationToken);
        }
        catch (TaskDispatchPreparationException exception)
        {
            await PersistPreparationFailedAsync(
                request,
                taskExecution,
                attempt,
                dispatch,
                exception,
                cancellationToken);
            return;
        }
        catch (Exception exception)
        {
            await PersistQueuePreparationFailedAsync(
                request,
                taskExecution,
                attempt,
                dispatch,
                exception,
                cancellationToken);
            return;
        }

        var preparedOnUtc = DateTime.UtcNow;
        dispatch.DispatchStatus = "QueuePending";
        request.Instance.LastUpdatedOnUtc = preparedOnUtc;
        await PersistDispatchGraphAsync(
            request,
            taskExecution,
            attempt,
            dispatch,
            ResolveQueuePreparedTransitionType(request.Kind, hasDeferredDispatch),
            ResolveQueuePreparedFromStatus(request.Kind, previousStatus),
            dispatch.DispatchStatus,
            preparedOnUtc,
            BuildQueuePreparedMessage(request, attemptNumber),
            preparedDispatch.RequestPayload,
            cancellationToken);

        await QueueAsync(
            request,
            executionPolicy,
            taskExecution,
            attempt,
            dispatch,
            preparedDispatch,
            hasDeferredDispatch,
            cancellationToken);
    }

    private async Task QueueAsync(
        TaskAttemptDispatchRequest request,
        ExecutionPolicyResolutionResult executionPolicy,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        PreparedDispatch preparedDispatch,
        bool hasDeferredDispatch,
        CancellationToken cancellationToken)
    {
        try
        {
            await executionPolicy.Provider.DispatchAsync(new ExecutionEnvelope
            {
                TaskRequest = preparedDispatch.CommandRequest,
                Command = preparedDispatch.Command,
                ResolvedPolicy = executionPolicy.ResolvedPolicy
            }, cancellationToken);

            await MarkDeliveryAcceptedAsync(
                request,
                taskExecution,
                attempt,
                dispatch,
                hasDeferredDispatch,
                cancellationToken);
        }
        catch (RemoteCommandConfigurationException exception)
        {
            await MarkQueueFailedAsync(request, taskExecution, attempt, dispatch, exception, cancellationToken);
        }
        catch (RemoteCommandDeliveryUncertainException exception)
        {
            await MarkDeliveryDeferredAsync(request, taskExecution, attempt, dispatch, exception, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await MarkQueueFailedAsync(request, taskExecution, attempt, dispatch, exception, cancellationToken);
        }
    }

    private async Task<PreparedDispatch> PrepareDispatchAsync(
        TaskAttemptDispatchRequest request,
        ITaskRuntimeAdapter adapter,
        ExecutionPolicyResolutionResult executionPolicy,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        DateTimeOffset? scheduledOnUtc,
        CancellationToken cancellationToken)
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
        var requestPayload = preparation.Payload;
        var commandPayload = requestPayload?.ToJsonString();
        attempt.RequestPayload = requestPayload?.DeepClone();
        dispatch.RequestPayload = requestPayload?.DeepClone();
        request.Instance.SnapshotPayload = _payloadState.ApplyTaskRequestPayload(
            request.Instance,
            request.StageKey,
            request.Task.Key,
            requestPayload);

        var commandRequest = new TaskRuntimeCommandRequest
        {
            Instance = request.Instance,
            StageExecutionId = request.StageExecutionId,
            StageKey = request.StageKey,
            Task = request.Task,
            TaskExecution = taskExecution,
            Attempt = attempt,
            Dispatch = dispatch,
            Payload = commandPayload
        };
        var descriptor = await adapter.BuildCommandAsync(commandRequest, cancellationToken);
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

        var policySnapshot = dispatch.Metadata.TryGetValue("ResolvedExecutionPolicy", out var resolvedPolicy)
            ? resolvedPolicy?.DeepClone()
            : null;
        dispatch.Metadata = RemoteCommandMetadataBuilder.Build(messageMetadata, propagationMetadata);
        if (policySnapshot is not null)
        {
            dispatch.Metadata["ResolvedExecutionPolicy"] = policySnapshot;
        }

        var command = new RemoteCommand
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
        };

        dispatch.Metadata[DeliveryProviderKeyMetadataKey] = JsonValue.Create(executionPolicy.ResolvedPolicy.ProviderKey);
        dispatch.Metadata[DeliveryCommandMetadataKey] = JsonSerializer.SerializeToNode(command, SerializerOptions);
        return new PreparedDispatch(commandRequest, command, requestPayload);
    }

    private async Task PersistDispatchGraphAsync(
        TaskAttemptDispatchRequest request,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        string transitionType,
        string fromStatus,
        string toStatus,
        DateTime occurredOnUtc,
        string message,
        JsonNode payload,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
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
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = request.Instance.Id,
                StageExecutionId = request.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = transitionType,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                OccurredOnUtc = occurredOnUtc,
                Message = message,
                Payload = payload?.DeepClone(),
                ProducedBy = nameof(TaskAttemptDispatcher)
            }, cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
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
            request.Instance.Status = OrchestrationInstanceStatus.Running;
            request.Instance.FailedOnUtc = null;
            request.Instance.ErrorSummary = message;
            request.Instance.LastUpdatedOnUtc = failedOnUtc;

            await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
            using (_storageUnitOfWork.DeferAutoSave())
            {
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
                await _storageUnitOfWork.SaveChanges(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
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
        request.Instance.Status = OrchestrationInstanceStatus.Running;
        request.Instance.CurrentStageKey = request.StageKey;
        request.Instance.CurrentTaskKey = request.Task.Key;
        request.Instance.FailedOnUtc = null;
        request.Instance.ErrorSummary = message;
        request.Instance.LastUpdatedOnUtc = failedOnUtc;
        request.Instance.WaitingSinceUtc = null;

        await using var initialTransaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
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
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await initialTransaction.CommitAsync(cancellationToken);
    }

    private async Task PersistPreparationFailedAsync(
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

        request.Instance.Status = OrchestrationInstanceStatus.Running;
        request.Instance.FailedOnUtc = null;
        request.Instance.ErrorSummary = exception.Message;
        request.Instance.LastUpdatedOnUtc = failedOnUtc;

        await PersistDispatchGraphAsync(
            request,
            taskExecution,
            attempt,
            dispatch,
            request.Kind == TaskAttemptDispatchKind.Initial
                ? "TaskDispatchPreparationFailed"
                : "TaskRetryPreparationFailed",
            TaskExecutionStatus.Running.ToString(),
            TaskExecutionStatus.Failed.ToString(),
            failedOnUtc,
            exception.Message,
            dispatch.RequestPayload,
            cancellationToken);
    }

    private async Task PersistQueuePreparationFailedAsync(
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
        request.Instance.Status = OrchestrationInstanceStatus.Running;
        request.Instance.FailedOnUtc = null;
        request.Instance.ErrorSummary = exception.Message;
        request.Instance.LastUpdatedOnUtc = failedOnUtc;

        await PersistDispatchGraphAsync(
            request,
            taskExecution,
            attempt,
            dispatch,
            request.Kind == TaskAttemptDispatchKind.Initial
                ? "TaskDispatchQueueFailed"
                : "TaskRetryQueueFailed",
            TaskExecutionStatus.Running.ToString(),
            TaskExecutionStatus.Failed.ToString(),
            failedOnUtc,
            exception.Message,
            dispatch.RequestPayload,
            cancellationToken);
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
        request.Instance.Status = OrchestrationInstanceStatus.Running;
        request.Instance.FailedOnUtc = null;
        request.Instance.ErrorSummary = exception.Message;
        request.Instance.LastUpdatedOnUtc = failedOnUtc;

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
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
                FromStatus = "QueuePending",
                ToStatus = TaskExecutionStatus.Failed.ToString(),
                OccurredOnUtc = failedOnUtc,
                Message = exception.Message,
                ProducedBy = nameof(TaskAttemptDispatcher)
            }, cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MarkDeliveryDeferredAsync(
        TaskAttemptDispatchRequest request,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var persistedDispatch = await _dispatchRepository.TryGetById(dispatch.Id, cancellationToken);
        if (persistedDispatch is null ||
            !string.Equals(persistedDispatch.DispatchStatus, "QueuePending", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var deferredOnUtc = DateTime.UtcNow;
        persistedDispatch.Metadata["Runtime.Delivery.LastEnqueueFailure"] = JsonValue.Create(exception.Message);
        persistedDispatch.Metadata["Runtime.Delivery.LastEnqueueAttemptOnUtc"] = JsonValue.Create(deferredOnUtc);

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _dispatchRepository.Update(persistedDispatch, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = request.Instance.Id,
                StageExecutionId = request.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchDeliveryDeferred",
                FromStatus = "QueuePending",
                ToStatus = "QueuePending",
                OccurredOnUtc = deferredOnUtc,
                Message = exception.Message,
                ProducedBy = nameof(TaskAttemptDispatcher)
            }, cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MarkDeliveryAcceptedAsync(
        TaskAttemptDispatchRequest request,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        bool hasDeferredDispatch,
        CancellationToken cancellationToken)
    {
        var persistedDispatch = await _dispatchRepository.TryGetById(dispatch.Id, cancellationToken);
        if (persistedDispatch is null ||
            !string.Equals(persistedDispatch.DispatchStatus, "QueuePending", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var acceptedOnUtc = DateTime.UtcNow;
        persistedDispatch.DispatchStatus = hasDeferredDispatch ? "Scheduled" : "Enqueued";
        persistedDispatch.FailedOnUtc = null;
        persistedDispatch.FailureReason = null;
        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _dispatchRepository.Update(persistedDispatch, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = request.Instance.Id,
                StageExecutionId = request.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchDeliveryAccepted",
                FromStatus = "QueuePending",
                ToStatus = persistedDispatch.DispatchStatus,
                OccurredOnUtc = acceptedOnUtc,
                Message = BuildQueuedMessage(
                    request,
                    attempt.AttemptNumber,
                    new DateTimeOffset(persistedDispatch.ScheduledOnUtc ?? acceptedOnUtc, TimeSpan.Zero),
                    persistedDispatch.DispatchStatus),
                ProducedBy = nameof(TaskAttemptDispatcher)
            }, cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
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

    private static string ResolveQueuePreparedTransitionType(TaskAttemptDispatchKind kind, bool hasDeferredDispatch)
        => ResolveQueuedTransitionType(kind, hasDeferredDispatch);

    private static string ResolveQueuePreparedFromStatus(TaskAttemptDispatchKind kind, TaskExecutionStatus previousStatus)
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

    private static string BuildQueuePreparedMessage(TaskAttemptDispatchRequest request, int attemptNumber)
        => request.Kind == TaskAttemptDispatchKind.Initial
            ? $"Task '{request.Task.Key}' durable delivery intent prepared."
            : $"Retry attempt {attemptNumber} durable delivery intent prepared for task '{request.Task.Key}'.";

    private static JsonNode ParsePayload(string payload)
        => string.IsNullOrWhiteSpace(payload) ? null : JsonNode.Parse(payload);

    private static void ApplyExecutionPolicySnapshot(
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        ResolvedExecutionPolicyArtifact policy)
    {
        var snapshot = JsonSerializer.SerializeToNode(policy, SerializerOptions);
        if (snapshot is not null)
        {
            attempt.Metadata["ResolvedExecutionPolicy"] = snapshot.DeepClone();
            dispatch.Metadata["ResolvedExecutionPolicy"] = snapshot.DeepClone();
        }

        taskExecution.Metadata["ResolvedExecutionProvider"] = JsonValue.Create(policy.ProviderKey);
        taskExecution.Metadata["ResolvedExecutionMode"] = JsonValue.Create(policy.ExecutionMode);
    }

    private static string GetSagaId(OrchestrationInstance instance)
        => string.IsNullOrWhiteSpace(instance.SagaId)
            ? instance.Id.ToString()
            : instance.SagaId;

    private sealed record PreparedDispatch(
        TaskRuntimeCommandRequest CommandRequest,
        RemoteCommand Command,
        JsonNode RequestPayload);
}
