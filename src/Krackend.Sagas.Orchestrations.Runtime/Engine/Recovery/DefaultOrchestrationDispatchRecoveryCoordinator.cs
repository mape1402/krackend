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

    public DefaultOrchestrationDispatchRecoveryCoordinator(
        ITaskDispatchRepository dispatchRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskExecutionRepository taskRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeStorageUnitOfWork storageUnitOfWork,
        IExecutionSandboxProviderRegistry providerRegistry,
        IOrchestrationInstanceCoordinator coordinator,
        ISagaEngine sagaEngine)
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
    }

    public async Task<int> RecoverPendingDispatchesAsync(int take, CancellationToken cancellationToken = default)
    {
        var candidates = await _dispatchRepository.GetQueuePending(Math.Max(1, take), cancellationToken);
        var recovered = 0;

        foreach (var candidate in candidates)
        {
            var attempt = await _attemptRepository.GetById(candidate.TaskExecutionAttemptId, cancellationToken);
            var task = attempt is null
                ? null
                : await _taskRepository.GetById(attempt.TaskExecutionId, cancellationToken);
            if (task is null)
            {
                continue;
            }

            var handled = false;
            var acquired = await _coordinator.TryExecuteAsync(
                task.OrchestrationInstanceId,
                async token => handled = await RecoverDispatchAsync(candidate.Id, token),
                cancellationToken,
                waitForLease: false);
            if (acquired && handled)
            {
                recovered++;
            }
        }

        return recovered;
    }

    private async Task<bool> RecoverDispatchAsync(Id dispatchId, CancellationToken cancellationToken)
    {
        var dispatch = await _dispatchRepository.TryGetById(dispatchId, cancellationToken);
        if (dispatch is null || !IsQueuePending(dispatch.DispatchStatus))
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

        if (!TryReadRecoveryRequest(dispatch, out var providerKey, out var command, out var reason))
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
        if (persisted is null || !IsQueuePending(persisted.DispatchStatus))
        {
            return;
        }

        var now = DateTime.UtcNow;
        persisted.DispatchStatus = command.ScheduledOnUtc is { } scheduledOnUtc && scheduledOnUtc > now
            ? "Scheduled"
            : "Enqueued";
        persisted.FailedOnUtc = null;
        persisted.FailureReason = null;
        persisted.Metadata["Runtime.Delivery.RecoveryStatus"] = JsonValue.Create(result.Status);

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _dispatchRepository.Update(persisted, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchRecovered",
                FromStatus = "QueuePending",
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
        if (persisted is null || !IsQueuePending(persisted.DispatchStatus))
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
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchRecoveryDeferred",
                FromStatus = "QueuePending",
                ToStatus = "QueuePending",
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
        dispatch.DispatchStatus = "Cancelled";
        dispatch.FailedOnUtc = DateTime.UtcNow;
        dispatch.FailureReason = "Dispatch is no longer the current executable task attempt.";
        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _dispatchRepository.Update(dispatch, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchRecoverySuppressed",
                FromStatus = "QueuePending",
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
        instance.Status = task.OnErrorPolicy == OnErrorPolicy.Continue
            ? OrchestrationInstanceStatus.Running
            : OrchestrationInstanceStatus.Failed;
        instance.FailedOnUtc = instance.Status == OrchestrationInstanceStatus.Failed ? now : null;
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
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchRecoveryFailed",
                FromStatus = "QueuePending",
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
           task.Status == TaskExecutionStatus.Running &&
           attempt.Status == TaskExecutionStatus.Running &&
           (task.LastAttemptNumber == 0 || task.LastAttemptNumber == attempt.AttemptNumber);

    private static bool IsQueuePending(string status)
        => string.Equals(status, "QueuePending", StringComparison.OrdinalIgnoreCase);

    private static bool IsTerminal(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Completed
            or OrchestrationInstanceStatus.CompletedWithErrors
            or OrchestrationInstanceStatus.DeadLettered
            or OrchestrationInstanceStatus.Aborted
            or OrchestrationInstanceStatus.Stopped
            or OrchestrationInstanceStatus.Compensated;

    private static bool TryReadRecoveryRequest(
        TaskDispatch dispatch,
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

        providerKey = providerNode.GetValue<string>();
        return true;
    }
}
