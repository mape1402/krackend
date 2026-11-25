namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Execution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

internal sealed class DefaultOrchestrationDispatchRecoveryCoordinator : IOrchestrationDispatchRecoveryCoordinator
{
    // Healthy direct delivery remains WaitingResponse with no SentOnUtc receipt for the
    // callback to acknowledge in the same functional commit. Replay only intents that
    // outlived the normal direct handoff; QueuePending is reserved for exceptional Mule
    // delivery and no longer receives one write per healthy task.
    private static readonly TimeSpan DirectHandoffRecoveryGrace = TimeSpan.FromSeconds(30);
    private const string DeliveryProviderKeyMetadataKey = "Runtime.Delivery.ProviderKey";
    private const string DeliveryCommandMetadataKey = "Runtime.Delivery.Command";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRuntimeStorageUnitOfWork _storageUnitOfWork;
    private readonly IExecutionSandboxProviderRegistry _providerRegistry;
    private readonly IOrchestrationInstanceCoordinator _coordinator;
    private readonly ISagaEngine _sagaEngine;
    private readonly IOrchestrationDurableWorkInspector _workInspector;

    public DefaultOrchestrationDispatchRecoveryCoordinator(
        ITaskDispatchRepository dispatchRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskExecutionRepository taskRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeStorageUnitOfWork storageUnitOfWork,
        IExecutionSandboxProviderRegistry providerRegistry,
        IOrchestrationInstanceCoordinator coordinator,
        ISagaEngine sagaEngine,
        IOrchestrationDurableWorkInspector workInspector = null)
    {
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _storageUnitOfWork = storageUnitOfWork ?? throw new ArgumentNullException(nameof(storageUnitOfWork));
        _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
        _workInspector = workInspector;
    }

    public async Task<int> RecoverPendingDispatchesAsync(int take, CancellationToken cancellationToken = default)
    {
        var candidates = await _dispatchRepository.GetQueuePending(Math.Max(1, take), cancellationToken);
        var recovered = 0;
        var recoveryCutoffUtc = DateTime.UtcNow.Subtract(DirectHandoffRecoveryGrace);

        foreach (var candidate in candidates)
        {
            if (IsFreshPreparedDirectIntent(candidate, recoveryCutoffUtc))
            {
                continue;
            }

            var attempt = await _attemptRepository.GetById(candidate.TaskExecutionAttemptId, cancellationToken);
            var task = attempt is null
                ? null
                : await _taskRepository.GetById(attempt.TaskExecutionId, cancellationToken);
            if (task is null)
            {
                continue;
            }

            // Prepared direct publication schedules its durable response timer only
            // after the transport confirms acceptance. That timer is therefore the
            // publication receipt for the optimized path, which deliberately avoids
            // a second dispatch-row update. Never republish while it is still live.
            if (candidate.SentOnUtc is null &&
                string.Equals(candidate.DispatchStatus, "WaitingResponse", StringComparison.OrdinalIgnoreCase) &&
                task.Status == TaskExecutionStatus.WaitingResponse &&
                attempt.Status == TaskExecutionStatus.WaitingResponse &&
                _workInspector is not null &&
                await _workInspector.InspectTimeoutAsync(task, attempt, cancellationToken) == DurableWorkPresence.Present)
            {
                continue;
            }

            var handled = false;
            var acquired = await _coordinator.TryExecuteAsync(
                task.OrchestrationInstanceId,
                async token => handled = await RecoverDispatchCoreAsync(candidate.Id, token),
                cancellationToken,
                waitForLease: false);
            if (acquired && handled)
            {
                recovered++;
            }
        }

        return recovered;
    }

    private static bool IsFreshPreparedDirectIntent(TaskDispatch dispatch, DateTime recoveryCutoffUtc)
    {
        if (dispatch.ScheduledOnUtc is not { } scheduledOnUtc || scheduledOnUtc <= recoveryCutoffUtc ||
            !dispatch.Metadata.TryGetValue(DeliveryCommandMetadataKey, out var commandNode) ||
            commandNode is not JsonObject command)
        {
            return false;
        }

        return command.TryGetPropertyValue("isPreparedDirectActivation", out var preparedNode) &&
               preparedNode?.GetValue<bool>() == true;
    }

    public async Task<bool> RecoverDispatchAsync(Id dispatchId, CancellationToken cancellationToken = default)
    {
        var dispatch = await _dispatchRepository.TryGetById(dispatchId, cancellationToken);
        if (dispatch is null) return false;
        var attempt = await _attemptRepository.GetById(dispatch.TaskExecutionAttemptId, cancellationToken);
        if (attempt is null) return false;
        var task = await _taskRepository.GetById(attempt.TaskExecutionId, cancellationToken);
        if (task is null) return false;
        var handled = false;
        var acquired = await _coordinator.TryExecuteAsync(task.OrchestrationInstanceId,
            async token => handled = await RecoverDispatchCoreAsync(dispatchId, token), cancellationToken, waitForLease: false);
        return acquired && handled;
    }

    private async Task<bool> RecoverDispatchCoreAsync(Id dispatchId, CancellationToken cancellationToken)
    {
        var dispatch = await _dispatchRepository.TryGetById(dispatchId, cancellationToken);
        if (dispatch is null || !IsRecoveryPending(dispatch))
        {
            return false;
        }

        var attempt = await _attemptRepository.GetById(dispatch.TaskExecutionAttemptId, cancellationToken);
        var task = attempt is null
            ? null
            : await _taskRepository.GetById(attempt.TaskExecutionId, cancellationToken);
        var instance = task is null
            ? null
            : await _instanceRepository.GetById(task.OrchestrationInstanceId, cancellationToken);
        if (attempt is null || task is null || instance is null)
        {
            return false;
        }

        if (!IsCurrentDispatch(instance, task, attempt))
        {
            await FenceStaleDispatchAsync(instance, task, attempt, dispatch, cancellationToken);
            return true;
        }

        if (!TryReadRecoveryRequest(dispatch, attempt, task, instance, out var providerKey, out var command, out var reason))
        {
            await FailDispatchRecoveryAsync(instance, task, attempt, dispatch, "DispatchRecoverySnapshotMissing", reason, cancellationToken);
            await AdvanceFailedInstanceAsync(instance, cancellationToken);
            return true;
        }

        if (!_providerRegistry.TryGet(providerKey, out var provider) ||
            provider is not IRecoverableExecutionSandboxProvider recoverableProvider)
        {
            await FailDispatchRecoveryAsync(
                instance,
                task,
                attempt,
                dispatch,
                "DispatchRecoveryUnsupported",
                $"Execution provider '{providerKey}' does not support durable dispatch recovery.",
                cancellationToken);
            await AdvanceFailedInstanceAsync(instance, cancellationToken);
            return true;
        }

        ExecutionDispatchRecoveryResult result;
        try
        {
            result = await recoverableProvider.RecoverDispatchAsync(
                new ExecutionDispatchRecoveryRequest
                {
                    Dispatch = dispatch,
                    Command = command
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RemoteCommandDeliveryUncertainException exception)
        {
            await MarkDispatchRecoveryDeferredAsync(instance, task, attempt, dispatch, exception, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            result = ExecutionDispatchRecoveryResult.Rejected("ProviderError", exception.Message);
        }

        if (!result.Succeeded)
        {
            await FailDispatchRecoveryAsync(instance, task, attempt, dispatch, result.Status, result.Message, cancellationToken);
            await AdvanceFailedInstanceAsync(instance, cancellationToken);
            return true;
        }

        await MarkDispatchRecoveredAsync(instance, task, attempt, dispatch, command, result, cancellationToken);
        return true;
    }

    private async Task MarkDispatchRecoveredAsync(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        RemoteCommand command,
        ExecutionDispatchRecoveryResult result,
        CancellationToken cancellationToken)
    {
        var persisted = await _dispatchRepository.TryGetById(dispatch.Id, cancellationToken);
        if (persisted is null || !IsRecoveryPending(persisted))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var previousStatus = persisted.DispatchStatus;
        persisted.DispatchStatus = command.AwaitResponse
            ? "WaitingResponse"
            : command.ScheduledOnUtc is { } scheduledOnUtc && scheduledOnUtc > now
                ? "Scheduled"
                : "Enqueued";
        persisted.SentOnUtc ??= command.AwaitResponse ? now : null;
        persisted.FailedOnUtc = null;
        persisted.FailureReason = null;
        persisted.Metadata["Runtime.Delivery.RecoveryStatus"] = JsonValue.Create(result.Status);

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _dispatchRepository.Update(persisted, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.NewDistributed(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchRecovered",
                FromStatus = previousStatus,
                ToStatus = persisted.DispatchStatus,
                OccurredOnUtc = now,
                Message = result.Message,
                ProducedBy = nameof(DefaultOrchestrationDispatchRecoveryCoordinator)
            }, cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MarkDispatchRecoveryDeferredAsync(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var persisted = await _dispatchRepository.TryGetById(dispatch.Id, cancellationToken);
        if (persisted is null || !IsRecoveryPending(persisted))
        {
            return;
        }

        var now = DateTime.UtcNow;
        persisted.Metadata["Runtime.Delivery.LastRecoveryFailure"] = JsonValue.Create(exception.Message);
        persisted.Metadata["Runtime.Delivery.LastRecoveryAttemptOnUtc"] = JsonValue.Create(now);

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _dispatchRepository.Update(persisted, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.NewDistributed(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchRecoveryDeferred",
                FromStatus = persisted.DispatchStatus,
                ToStatus = persisted.DispatchStatus,
                OccurredOnUtc = now,
                Message = exception.Message,
                ProducedBy = nameof(DefaultOrchestrationDispatchRecoveryCoordinator)
            }, cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task FenceStaleDispatchAsync(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        CancellationToken cancellationToken)
    {
        var previousStatus = dispatch.DispatchStatus;
        dispatch.DispatchStatus = "Cancelled";
        dispatch.FailedOnUtc = DateTime.UtcNow;
        dispatch.FailureReason = "Dispatch is no longer the current executable task attempt.";
        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _dispatchRepository.Update(dispatch, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.NewDistributed(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchRecoverySuppressed",
                FromStatus = previousStatus,
                ToStatus = "Cancelled",
                OccurredOnUtc = dispatch.FailedOnUtc.Value,
                Message = dispatch.FailureReason,
                ProducedBy = nameof(DefaultOrchestrationDispatchRecoveryCoordinator)
            }, cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task FailDispatchRecoveryAsync(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        string errorCode,
        string message,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var previousStatus = dispatch.DispatchStatus;
        dispatch.DispatchStatus = "Failed";
        dispatch.FailedOnUtc = now;
        dispatch.FailureReason = message;
        dispatch.Metadata["Runtime.Delivery.RecoveryStatus"] = JsonValue.Create(errorCode);
        task.Status = TaskExecutionStatus.Failed;
        task.FailedOnUtc = now;
        task.WaitingSinceUtc = null;
        attempt.Status = TaskExecutionStatus.Failed;
        attempt.FailedOnUtc = now;
        attempt.WaitingSinceUtc = null;
        attempt.ErrorCode = errorCode;
        attempt.ErrorMessage = message;
        instance.Status = OrchestrationInstanceStatus.Running;
        instance.FailedOnUtc = null;
        instance.WaitingSinceUtc = null;
        instance.ErrorSummary = message;
        instance.LastUpdatedOnUtc = now;

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _dispatchRepository.Update(dispatch, cancellationToken);
            await _attemptRepository.Update(attempt, cancellationToken);
            await _taskRepository.Update(task, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.NewDistributed(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchRecoveryFailed",
                FromStatus = previousStatus,
                ToStatus = "Failed",
                OccurredOnUtc = now,
                Message = message,
                ProducedBy = nameof(DefaultOrchestrationDispatchRecoveryCoordinator)
            }, cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task AdvanceFailedInstanceAsync(OrchestrationInstance instance, CancellationToken cancellationToken)
        => await _sagaEngine.OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                SagaId = string.IsNullOrWhiteSpace(instance.SagaId) ? instance.Id.ToString() : instance.SagaId,
                OrchestrationInstanceId = instance.Id.ToString(),
                CurrentStage = instance.CurrentStageKey,
                CurrentTasks = string.IsNullOrWhiteSpace(instance.CurrentTaskKey) ? Array.Empty<string>() : [instance.CurrentTaskKey],
                CorrelationId = instance.CorrelationId
            },
            Payload = instance.SnapshotPayload?.DeepClone()
        }, cancellationToken);

    private static bool IsCurrentDispatch(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt)
        => !IsTerminal(instance.Status) &&
           (task.Status == TaskExecutionStatus.Running && attempt.Status == TaskExecutionStatus.Running ||
            task.Status == TaskExecutionStatus.WaitingResponse && attempt.Status == TaskExecutionStatus.WaitingResponse) &&
           (task.LastAttemptNumber == 0 || task.LastAttemptNumber == attempt.AttemptNumber);

    private static bool IsRecoveryPending(TaskDispatch dispatch)
        => string.Equals(dispatch.DispatchStatus, "QueuePending", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(dispatch.DispatchStatus, "WaitingResponse", StringComparison.OrdinalIgnoreCase) &&
           dispatch.SentOnUtc is null;

    private static bool IsTerminal(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Completed
            or OrchestrationInstanceStatus.CompletedWithErrors
            or OrchestrationInstanceStatus.DeadLettered
            or OrchestrationInstanceStatus.Aborted
            or OrchestrationInstanceStatus.Stopped
            or OrchestrationInstanceStatus.Compensated;

    private static bool TryReadRecoveryRequest(
        TaskDispatch dispatch,
        TaskExecutionAttempt attempt,
        TaskExecution task,
        OrchestrationInstance instance,
        out string providerKey,
        out RemoteCommand command,
        out string reason)
    {
        providerKey = string.Empty;
        command = null!;
        reason = $"Dispatch '{dispatch.Id}' does not contain a recoverable delivery snapshot.";
        if (!dispatch.Metadata.TryGetValue(DeliveryProviderKeyMetadataKey, out var providerNode) ||
            string.IsNullOrWhiteSpace(providerNode?.GetValue<string>()))
        {
            return false;
        }

        if (!dispatch.Metadata.TryGetValue(DeliveryCommandMetadataKey, out var commandNode) || commandNode is null)
        {
            return false;
        }

        try
        {
            command = commandNode.Deserialize<RemoteCommand>(SerializerOptions)!;
        }
        catch (JsonException)
        {
            reason = $"Dispatch '{dispatch.Id}' contains an invalid durable delivery snapshot.";
            return false;
        }

        if (command is null)
        {
            return false;
        }

        if (command.MessageMetadata is null)
        {
            if (dispatch.Metadata.TryGetValue(
                    OrchestrationMetadataConstants.OrchestrationMessageMetadataKey,
                    out var messageMetadataNode) &&
                messageMetadataNode is not null)
            {
                try
                {
                    command.MessageMetadata = messageMetadataNode.Deserialize<OrchestrationMessageMetadata>(SerializerOptions);
                }
                catch (JsonException)
                {
                    reason = $"Dispatch '{dispatch.Id}' contains invalid orchestration message metadata.";
                    return false;
                }
            }

            // Older recovery snapshots did not always persist the transport envelope.
            // Preserve their replay compatibility with the ownership graph we have
            // already validated above.
            command.MessageMetadata ??= new OrchestrationMessageMetadata
            {
                SagaId = string.IsNullOrWhiteSpace(instance.SagaId) ? instance.Id.ToString() : instance.SagaId,
                OrchestrationInstanceId = instance.Id.ToString(),
                CurrentStage = command.StageKey,
                CurrentTasks = [task.TaskKey],
                CorrelationId = string.IsNullOrWhiteSpace(task.CorrelationId)
                    ? dispatch.CorrelationId
                    : task.CorrelationId,
                TaskExecutionId = task.Id.ToString(),
                DispatchId = dispatch.Id.ToString(),
                Attempt = attempt.AttemptNumber
            };
        }

        if (command.PropagationMetadata is not { HasItems: true })
        {
            command.PropagationMetadata = new OrchestrationPropagationMetadata();
            foreach (var item in dispatch.Metadata)
            {
                if (string.Equals(
                        item.Key,
                        OrchestrationMetadataConstants.OrchestrationMessageMetadataKey,
                        StringComparison.Ordinal) ||
                    string.Equals(item.Key, DeliveryProviderKeyMetadataKey, StringComparison.Ordinal) ||
                    string.Equals(item.Key, DeliveryCommandMetadataKey, StringComparison.Ordinal) ||
                    string.Equals(item.Key, "ResolvedExecutionPolicy", StringComparison.Ordinal) ||
                    item.Key.StartsWith("Runtime.", StringComparison.Ordinal) ||
                    item.Key.StartsWith("Mule.", StringComparison.Ordinal) ||
                    item.Key.StartsWith("Recovery.", StringComparison.Ordinal) ||
                    RemoteCommandMetadataBuilder.IsReserved(item.Key))
                {
                    continue;
                }

                command.PropagationMetadata.Items[item.Key] = item.Value?.DeepClone();
            }
        }

        // New snapshots intentionally omit the duplicated payload. The attempt is
        // part of the same durable dispatch graph and is its canonical owner.
        if (string.IsNullOrWhiteSpace(command.Payload) && attempt.RequestPayload is not null)
            command.Payload = attempt.RequestPayload.ToJsonString();

        providerKey = providerNode.GetValue<string>();
        return true;
    }
}
