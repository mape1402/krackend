namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Execution;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IExecutionSandboxProviderRegistry _executionProviderRegistry;
    private readonly IRuntimeStorageUnitOfWork _storageUnitOfWork;
    private readonly IOrchestrationInstanceCoordinator _instanceCoordinator;

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
        IExecutionSandboxProviderRegistry executionProviderRegistry,
        IRuntimeStorageUnitOfWork storageUnitOfWork,
        IOrchestrationPropagationMetadataStore propagationMetadataStore = null,
        IOrchestrationInstanceCoordinator instanceCoordinator = null)
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
        _executionProviderRegistry = executionProviderRegistry ?? throw new ArgumentNullException(nameof(executionProviderRegistry));
        _storageUnitOfWork = storageUnitOfWork ?? throw new ArgumentNullException(nameof(storageUnitOfWork));
        _propagationMetadataStore = propagationMetadataStore ?? new DefaultOrchestrationPropagationMetadataStore();
        _instanceCoordinator = instanceCoordinator;
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
            Id = Id.NewDistributed(),
            TaskExecutionId = taskExecution.Id,
            AttemptNumber = attemptNumber,
            Status = TaskExecutionStatus.Running,
            StartedOnUtc = now,
            RequestPayload = requestPayload?.DeepClone()
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.NewDistributed(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = request.Task.DispatchType.ToString(),
            Destination = adapter.GetDestination(request.Task),
            RequestPayload = requestPayload?.DeepClone(),
            DispatchStatus = "Scheduled",
            CommandId = Id.NewDistributed().ToString(),
            CorrelationId = ResolveCorrelationId(request.Instance, taskExecution),
            ScheduledOnUtc = scheduledOnUtc.UtcDateTime
        };

        if (_dispatchRepository is IEmbeddedTaskTransportStateRepository
            {
                EmbedsCurrentTransportState: true
            })
        {
            // The current task, attempt and dispatch share one durable identity when
            // storage embeds the transport graph. Callback metadata can therefore
            // address the task directly and recovery can reconstruct both projections
            // without relational joins or two extra inserts.
            attempt.Id = taskExecution.Id;
            dispatch.Id = taskExecution.Id;
            dispatch.TaskExecutionAttemptId = taskExecution.Id;
        }
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
        var waitsForImmediateResponse = !hasDeferredDispatch &&
            request.Task.DispatchType != TaskDispatchType.FireAndForget;
        if (waitsForImmediateResponse)
        {
            // The dispatch graph is the durable publication intent. Arm the response
            // state in that same transaction instead of writing task + attempt again
            // after the transport accepts the command. Publication confirmation then
            // only has to stamp the independent dispatch row.
            PrepareImmediateResponseWait(request, taskExecution, attempt, preparedOnUtc);
            preparedDispatch.Command.PreparedResponseDeadlineUtc = taskExecution.ResponseDeadlineUtc;
            preparedDispatch.Command.IsPreparedDirectActivation = true;
            // Recovery deserializes the command snapshot from the dispatch row. Persist
            // the direct-activation hints with that snapshot, while queued replays still
            // perform their normal authoritative validation.
            dispatch.Metadata[DeliveryCommandMetadataKey] = SerializeRecoveryCommand(preparedDispatch.Command);
        }
        // A healthy response-bearing command is already armed for its callback in this
        // transaction. Do not insert it into QueuePending: that filtered index belongs
        // to exceptional Mule/recovery work, not to every successful orchestration.
        // SentOnUtc == null remains the durable crash-recovery marker until either the
        // callback folds publication confirmation into its commit or recovery republishes.
        dispatch.DispatchStatus = waitsForImmediateResponse ? "WaitingResponse" : "QueuePending";
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
        request.State?.TrackTask(taskExecution);
        request.StateUpdated = request.State is not null;

        var activation = new TaskDispatchActivation(
            request,
            taskExecution,
            attempt,
            dispatch,
            executionPolicy.Provider.ProviderKey,
            new ExecutionEnvelope
            {
                TaskRequest = preparedDispatch.CommandRequest,
                Command = preparedDispatch.Command,
                ResolvedPolicy = executionPolicy.ResolvedPolicy
            },
            hasDeferredDispatch);

        if (_instanceCoordinator is IOrchestrationPostLeaseWork postLeaseWork)
            await postLeaseWork.ExecuteIsolatedAfterLeaseReleaseAsync(
                request.Instance.Id,
                (services, token) => services.GetRequiredService<ITaskAttemptDispatcher>()
                    .ActivateAsync(activation, token),
                cancellationToken);
        else
            await ActivateAsync(activation, cancellationToken);
    }

    public async Task ActivateAsync(
        TaskDispatchActivation activation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activation);
        var request = activation.Request;
        var taskExecution = activation.TaskExecution;
        var attempt = activation.Attempt;
        var dispatch = activation.Dispatch;

        try
        {
            if (!_executionProviderRegistry.TryGet(activation.ProviderKey, out var provider))
            {
                throw new RemoteCommandConfigurationException(
                    $"Execution provider '{activation.ProviderKey}' is not available in the activation scope.");
            }

            await provider.DispatchAsync(activation.Envelope, cancellationToken);

            if (provider is not IExecutionDispatchLifecycleProvider { OwnsDispatchLifecycle: true })
            {
                await MutateAfterActivationAsync(request.Instance.Id, token => MarkDeliveryAcceptedAsync(
                    request, taskExecution, attempt, dispatch, activation.HasDeferredDispatch, token), cancellationToken);
            }
        }
        catch (RemoteCommandConfigurationException exception)
        {
            await MutateAfterActivationAsync(request.Instance.Id, token => MarkQueueFailedAsync(request, taskExecution, attempt, dispatch, exception, token), cancellationToken);
        }
        catch (RemoteCommandDeliveryUncertainException exception)
        {
            await MutateAfterActivationAsync(request.Instance.Id, token => MarkDeliveryDeferredAsync(request, taskExecution, attempt, dispatch, exception, token), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RuntimeStateConflictException) { throw; }
        catch (Exception exception)
        {
            await MutateAfterActivationAsync(request.Instance.Id, token => MarkQueueFailedAsync(request, taskExecution, attempt, dispatch, exception, token), cancellationToken);
        }
    }

    private async Task MutateAfterActivationAsync(Id instanceId, Func<CancellationToken, Task> mutation, CancellationToken token)
    {
        if (_instanceCoordinator is null) await mutation(token);
        else await _instanceCoordinator.TryExecuteAsync(instanceId, mutation, token, waitForLease: false);
        // Another advance can win; the persisted delivery intent remains recoverable.
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

        dispatch.Metadata = RemoteCommandMetadataBuilder.Build(messageMetadata, propagationMetadata);

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
        // The attempt row is the single durable owner of the request payload. The
        // recovery command stores transport/routing state only; persisting the same
        // growing JSON in the attempt, dispatch and serialized command made one task
        // fan out into multiple large SQL writes.
        dispatch.Metadata[DeliveryCommandMetadataKey] = SerializeRecoveryCommand(command);
        dispatch.RequestPayload = null;
        return new PreparedDispatch(commandRequest, command, requestPayload);
    }

    private static JsonNode SerializeRecoveryCommand(RemoteCommand command)
    {
        var snapshot = JsonSerializer.SerializeToNode(command, SerializerOptions)
            ?? throw new InvalidOperationException("The durable delivery command could not be serialized.");
        if (snapshot is JsonObject commandObject)
        {
            commandObject.Remove("payload");
            // Message and propagation metadata already have canonical entries in the
            // dispatch metadata envelope. Recovery rehydrates them from there instead
            // of persisting a second full copy inside the command snapshot.
            commandObject.Remove("messageMetadata");
            commandObject.Remove("propagationMetadata");
        }
        return snapshot;
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
        // A coordinated engine activation already owns a deferred unit-of-work and
        // commits before post-lease publication runs. Do not flush each dispatch graph
        // inside that transaction: parallel task decisions would otherwise turn one
        // callback into N SQL batches plus the final batch. Standalone callers still
        // own and flush their transaction here.
        var ownsFlush = _storageUnitOfWork.AutoSaveChanges;
        if (!ownsFlush)
        {
            await PersistDispatchGraphChangesAsync(
                request,
                taskExecution,
                attempt,
                dispatch,
                transitionType,
                fromStatus,
                toStatus,
                occurredOnUtc,
                message,
                payload,
                cancellationToken);
            return;
        }

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await PersistDispatchGraphChangesAsync(
                request,
                taskExecution,
                attempt,
                dispatch,
                transitionType,
                fromStatus,
                toStatus,
                occurredOnUtc,
                message,
                payload,
                cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task PersistDispatchGraphChangesAsync(
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
        if (request.Kind == TaskAttemptDispatchKind.Initial)
            await _taskRepository.Create(taskExecution, cancellationToken);
        else
            await _taskRepository.Update(taskExecution, cancellationToken);

        await _attemptRepository.Create(attempt, cancellationToken);
        await _dispatchRepository.Create(dispatch, cancellationToken);
        await _instanceRepository.Update(request.Instance, cancellationToken);
        await _transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.NewDistributed(),
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
                    Id = Id.NewDistributed(),
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
        request.Instance.IdleSinceUtc = null;
        request.Instance.IdleExpiresOnUtc = null;
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
                Id = Id.NewDistributed(),
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
        var persisted = await _dispatchRepository.TryGetById(dispatch.Id, cancellationToken);
        if (persisted is null || !string.Equals(persisted.DispatchStatus, "QueuePending", StringComparison.OrdinalIgnoreCase)) return;
        dispatch = persisted;
        taskExecution = await _taskRepository.GetById(taskExecution.Id, cancellationToken);
        attempt = await _attemptRepository.GetById(attempt.Id, cancellationToken);
        var instance = await _instanceRepository.GetById(request.Instance.Id, cancellationToken);
        if (taskExecution.LastAttemptNumber != attempt.AttemptNumber) return;
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
        instance.Status = OrchestrationInstanceStatus.Running;
        instance.FailedOnUtc = null;
        instance.ErrorSummary = exception.Message;
        instance.LastUpdatedOnUtc = failedOnUtc;

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _dispatchRepository.Update(dispatch, cancellationToken);
            await _attemptRepository.Update(attempt, cancellationToken);
            await _taskRepository.Update(taskExecution, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.NewDistributed(),
                OrchestrationInstanceId = instance.Id,
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
                Id = Id.NewDistributed(),
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
                Id = Id.NewDistributed(),
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
            Id = Id.NewDistributed(),
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
                ? Id.NewDistributed().ToString()
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

    private static void PrepareImmediateResponseWait(
        TaskAttemptDispatchRequest request,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        DateTime waitingSinceUtc)
    {
        taskExecution.Status = TaskExecutionStatus.WaitingResponse;
        taskExecution.WaitingSinceUtc = waitingSinceUtc;
        taskExecution.ResponseDeadlineUtc = request.Task.TimeoutPolicy is { } timeoutPolicy
            ? waitingSinceUtc.Add(timeoutPolicy.Timeout.Value)
            : null;
        taskExecution.TimeoutActionId = null;
        taskExecution.TimeoutActionLane = null;
        attempt.Status = TaskExecutionStatus.WaitingResponse;
        attempt.WaitingSinceUtc = waitingSinceUtc;
    }

    private static string ResolveCorrelationId(OrchestrationInstance instance, TaskExecution taskExecution)
        => string.IsNullOrWhiteSpace(taskExecution.CorrelationId)
            ? string.IsNullOrWhiteSpace(instance.CorrelationId) ? Id.NewDistributed().ToString() : instance.CorrelationId
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

internal sealed record TaskDispatchActivation(
    TaskAttemptDispatchRequest Request,
    TaskExecution TaskExecution,
    TaskExecutionAttempt Attempt,
    TaskDispatch Dispatch,
    string ProviderKey,
    ExecutionEnvelope Envelope,
    bool HasDeferredDispatch);
