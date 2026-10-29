using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

internal sealed class DefaultOrchestrationDispatchCancellationCoordinator :
    IOrchestrationDispatchCancellationCoordinator,
    IOrchestrationDispatchCancellationRecoveryCoordinator
{
    private const string CancellationReasonMetadataKey = "Recovery.DispatchCancellationReason";
    private const string CancellationSourceStatusMetadataKey = "Recovery.DispatchCancellationSourceStatus";
    private const string CancellationRequestedOnUtcMetadataKey = "Recovery.DispatchCancellationRequestedOnUtc";
    private const string CancellationLastAttemptOnUtcMetadataKey = "Recovery.DispatchCancellationLastAttemptOnUtc";
    private const string CancellationCompletedOnUtcMetadataKey = "Recovery.DispatchCancellationCompletedOnUtc";
    private const string CancellationProviderStatusMetadataKey = "Recovery.DispatchCancellationProviderStatus";
    private const string CancellationProviderMessageMetadataKey = "Recovery.DispatchCancellationProviderMessage";

    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRuntimeStorageUnitOfWork _storageUnitOfWork;
    private readonly ITaskDispatchCancellationService _cancellationService;
    private readonly ITaskDispatchCancellationRepository _cancellationRepository;
    private readonly IOrchestrationInstanceCoordinator _coordinator;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IOrchestrationDispatchCancellationIntentRepository _intentRepository;

    public DefaultOrchestrationDispatchCancellationCoordinator(
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeStorageUnitOfWork storageUnitOfWork,
        ITaskDispatchCancellationService cancellationService,
        IOrchestrationInstanceCoordinator coordinator = null,
        IOrchestrationInstanceRepository instanceRepository = null)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _storageUnitOfWork = storageUnitOfWork ?? throw new ArgumentNullException(nameof(storageUnitOfWork));
        _cancellationService = cancellationService ?? throw new ArgumentNullException(nameof(cancellationService));
        _cancellationRepository = dispatchRepository as ITaskDispatchCancellationRepository;
        _coordinator = coordinator;
        _instanceRepository = instanceRepository;
        _intentRepository = instanceRepository as IOrchestrationDispatchCancellationIntentRepository;
    }

    public async Task<int> CancelPendingAsync(
        OrchestrationInstance instance,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "Orchestration instance is no longer eligible to dispatch work."
            : reason.Trim();
        var now = DateTime.UtcNow;
        var cancellationRequests = new List<Id>();
        var tasks = await _taskRepository.GetByInstanceId(instance.Id, cancellationToken);

        await using (var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken))
        using (_storageUnitOfWork.DeferAutoSave())
        {
            foreach (var task in tasks)
            {
                var attempts = await _attemptRepository.GetByTaskExecutionId(task.Id, cancellationToken);
                foreach (var attempt in attempts)
                {
                    if (attempt.DispatchId is not { } dispatchId)
                    {
                        continue;
                    }

                    var dispatch = await _dispatchRepository.TryGetById(dispatchId, cancellationToken);
                    if (dispatch is null || dispatch.PhysicalCancellationCompletedOnUtc is not null)
                    {
                        continue;
                    }

                    var previousDispatchStatus = dispatch.DispatchStatus;
                    var requiresLogicalFence = IsPending(dispatch.DispatchStatus);
                    if (requiresLogicalFence)
                    {
                        dispatch.DispatchStatus = "Cancelled";
                        dispatch.FailedOnUtc = now;
                        dispatch.FailureReason = normalizedReason;
                    }

                    dispatch.PhysicalCancellationPending = true;
                    dispatch.PhysicalCancellationAttemptCount = 0;
                    dispatch.PhysicalCancellationLastAttemptOnUtc = null;
                    dispatch.PhysicalCancellationCompletedOnUtc = null;
                    dispatch.Metadata[CancellationReasonMetadataKey] = JsonValue.Create(normalizedReason);
                    dispatch.Metadata[CancellationSourceStatusMetadataKey] = JsonValue.Create(previousDispatchStatus);
                    dispatch.Metadata[CancellationRequestedOnUtcMetadataKey] = JsonValue.Create(now);
                    dispatch.Metadata.Remove(CancellationLastAttemptOnUtcMetadataKey);
                    dispatch.Metadata.Remove(CancellationCompletedOnUtcMetadataKey);
                    dispatch.Metadata.Remove(CancellationProviderStatusMetadataKey);
                    dispatch.Metadata.Remove(CancellationProviderMessageMetadataKey);

                    if (requiresLogicalFence && attempt.Status is (TaskExecutionStatus.Running or TaskExecutionStatus.Retrying))
                    {
                        attempt.Status = TaskExecutionStatus.Cancelled;
                        attempt.FailedOnUtc = now;
                        attempt.ErrorCode = "DispatchCancelled";
                        attempt.ErrorMessage = normalizedReason;
                    }

                    if (requiresLogicalFence && task.Status is (TaskExecutionStatus.Running or TaskExecutionStatus.Retrying))
                    {
                        task.Status = TaskExecutionStatus.Cancelled;
                        task.FailedOnUtc = now;
                        task.WaitingSinceUtc = null;
                    }

                    await _dispatchRepository.Update(dispatch, cancellationToken);
                    await _attemptRepository.Update(attempt, cancellationToken);
                    await _taskRepository.Update(task, cancellationToken);
                    if (requiresLogicalFence)
                    {
                        await _transitionRepository.Create(new ExecutionTransition
                        {
                            Id = Id.New(),
                            OrchestrationInstanceId = instance.Id,
                            StageExecutionId = task.StageExecutionId,
                            TaskExecutionId = task.Id,
                            TaskExecutionAttemptId = attempt.Id,
                            TransitionType = "TaskDispatchCancelled",
                            FromStatus = previousDispatchStatus,
                            ToStatus = "Cancelled",
                            OccurredOnUtc = now,
                            Message = normalizedReason,
                            ProducedBy = nameof(DefaultOrchestrationDispatchCancellationCoordinator)
                        }, cancellationToken);
                    }

                    cancellationRequests.Add(dispatch.Id);
                }
            }

            if (instance.DispatchCancellationPending && _instanceRepository is not null)
            {
                instance.DispatchCancellationPending = false;
                await _instanceRepository.Update(instance, cancellationToken);
            }

            await _storageUnitOfWork.SaveChanges(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        foreach (var dispatchId in cancellationRequests)
        {
            await ReconcileAsync(dispatchId, cancellationToken);
        }

        return cancellationRequests.Count;
    }

    public async Task<int> ReconcilePendingAsync(int take, CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Max(1, take);
        if (_intentRepository is not null && _coordinator is not null)
        {
            var offset = 0;
            var processed = 0;
            while (processed < batchSize)
            {
                var instances = await _intentRepository.GetDispatchCancellationPending(batchSize, offset, cancellationToken);
                if (instances.Count == 0)
                {
                    break;
                }

                var acquiredCount = 0;
                foreach (var candidate in instances)
                {
                    var acquired = await _coordinator.TryExecuteAsync(candidate.Id, async token =>
                    {
                        var instance = await _instanceRepository.GetById(candidate.Id, token);
                        if (!instance.DispatchCancellationPending)
                        {
                            return;
                        }

                        if (instance.Status is OrchestrationInstanceStatus.Completed or OrchestrationInstanceStatus.CompletedWithErrors
                            or OrchestrationInstanceStatus.Failed or OrchestrationInstanceStatus.DeadLettered
                            or OrchestrationInstanceStatus.Idle or OrchestrationInstanceStatus.Compensating
                            or OrchestrationInstanceStatus.Compensated or OrchestrationInstanceStatus.Aborted or OrchestrationInstanceStatus.Stopped)
                        {
                            await CancelPendingAsync(instance, "Recovered pending dispatch cancellation after an orchestration state change.", token);
                        }
                        else
                        {
                            // A late callback may have legitimately resumed the orchestration.
                            instance.DispatchCancellationPending = false;
                            await _instanceRepository.Update(instance, token);
                        }
                    }, cancellationToken, waitForLease: false);
                    if (acquired)
                    {
                        acquiredCount++;
                        processed++;
                    }

                    if (processed >= batchSize)
                    {
                        break;
                    }
                }

                offset = acquiredCount > 0 ? 0 : offset + instances.Count;
                if (instances.Count < batchSize)
                {
                    break;
                }
            }
        }

        if (_cancellationRepository is null)
        {
            return 0;
        }

        var reconciled = 0;
        var skip = 0;
        var attemptedBefore = DateTime.UtcNow.AddSeconds(-5);
        while (reconciled < batchSize)
        {
            var candidates = await _cancellationRepository.GetPhysicalCancellationPending(
                batchSize, skip, attemptedBefore, cancellationToken);
            if (candidates.Count == 0)
            {
                break;
            }

            var completed = 0;
            foreach (var candidate in candidates)
            {
                if (await ReconcileAsync(candidate.Id, cancellationToken))
                {
                    completed++;
                    reconciled++;
                }
            }

            skip = completed > 0 ? 0 : skip + candidates.Count;
            if (candidates.Count < batchSize)
            {
                break;
            }
        }

        return reconciled;
    }

    public async Task<bool> ReconcileAsync(Id dispatchId, CancellationToken cancellationToken = default)
    {
        var dispatch = await _dispatchRepository.TryGetById(dispatchId, cancellationToken);
        if (dispatch is null || !dispatch.PhysicalCancellationPending)
        {
            return false;
        }

        var attempt = await _attemptRepository.GetById(dispatch.TaskExecutionAttemptId, cancellationToken);
        var task = attempt is null
            ? null
            : await _taskRepository.GetById(attempt.TaskExecutionId, cancellationToken);
        if (task is null)
        {
            return false;
        }

        if (_coordinator is null)
        {
            return await ReconcileCoreAsync(dispatchId, cancellationToken);
        }

        var reconciled = false;
        var acquired = await _coordinator.TryExecuteAsync(
            task.OrchestrationInstanceId,
            async token => reconciled = await ReconcileCoreAsync(dispatchId, token),
            cancellationToken,
            waitForLease: false);
        return acquired && reconciled;
    }

    private async Task<bool> ReconcileCoreAsync(Id dispatchId, CancellationToken cancellationToken)
    {
        var dispatch = await _dispatchRepository.TryGetById(dispatchId, cancellationToken);
        if (dispatch is null || !dispatch.PhysicalCancellationPending)
        {
            return false;
        }

        var attempt = await _attemptRepository.GetById(dispatch.TaskExecutionAttemptId, cancellationToken);
        var task = attempt is null
            ? null
            : await _taskRepository.GetById(attempt.TaskExecutionId, cancellationToken);
        if (task is null)
        {
            return false;
        }

        var reason = dispatch.Metadata.TryGetValue(CancellationReasonMetadataKey, out var reasonNode)
            ? reasonNode?.GetValue<string>()
            : null;
        var result = await RequestPhysicalCancellationAsync(
            dispatch,
            string.IsNullOrWhiteSpace(reason) ? "Orchestration instance is no longer eligible to dispatch work." : reason,
            cancellationToken);
        var now = DateTime.UtcNow;

        dispatch.PhysicalCancellationAttemptCount++;
        dispatch.PhysicalCancellationLastAttemptOnUtc = now;
        dispatch.Metadata[CancellationLastAttemptOnUtcMetadataKey] = JsonValue.Create(now);
        dispatch.Metadata[CancellationProviderStatusMetadataKey] = JsonValue.Create(result.Status);
        dispatch.Metadata[CancellationProviderMessageMetadataKey] = JsonValue.Create(result.Message);
        if (result.IsTerminal)
        {
            dispatch.PhysicalCancellationPending = false;
            dispatch.PhysicalCancellationCompletedOnUtc = now;
            dispatch.Metadata[CancellationCompletedOnUtcMetadataKey] = JsonValue.Create(now);
        }

        await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
        using (_storageUnitOfWork.DeferAutoSave())
        {
            await _dispatchRepository.Update(dispatch, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = task.OrchestrationInstanceId,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = result.IsTerminal
                    ? "TaskDispatchPhysicalCancellationFinalized"
                    : "TaskDispatchPhysicalCancellationDeferred",
                FromStatus = dispatch.DispatchStatus,
                ToStatus = dispatch.DispatchStatus,
                OccurredOnUtc = now,
                Message = result.Message,
                ProducedBy = nameof(DefaultOrchestrationDispatchCancellationCoordinator)
            }, cancellationToken);
            await _storageUnitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<TaskDispatchCancellationResult> RequestPhysicalCancellationAsync(
        TaskDispatch dispatch,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _cancellationService.CancelAsync(
                new TaskDispatchCancellationRequest
                {
                    Dispatch = dispatch,
                    Reason = reason
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return TaskDispatchCancellationResult.Deferred("ProviderError", exception.Message);
        }
    }

    private static bool IsPending(string dispatchStatus)
        => string.Equals(dispatchStatus, "QueuePending", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(dispatchStatus, "Enqueued", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(dispatchStatus, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(dispatchStatus, "RetryPending", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(dispatchStatus, "ReplayPending", StringComparison.OrdinalIgnoreCase);
}
