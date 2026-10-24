using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

internal sealed class DefaultOrchestrationDispatchCancellationCoordinator : IOrchestrationDispatchCancellationCoordinator
{
    private const string CancellationReasonMetadataKey = "Recovery.DispatchCancellationReason";
    private const string CancellationRequestedOnUtcMetadataKey = "Recovery.DispatchCancellationRequestedOnUtc";
    private const string CancellationProviderStatusMetadataKey = "Recovery.DispatchCancellationProviderStatus";
    private const string CancellationProviderMessageMetadataKey = "Recovery.DispatchCancellationProviderMessage";

    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRuntimeStorageUnitOfWork _storageUnitOfWork;
    private readonly ITaskDispatchCancellationService _cancellationService;

    public DefaultOrchestrationDispatchCancellationCoordinator(
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeStorageUnitOfWork storageUnitOfWork,
        ITaskDispatchCancellationService cancellationService)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _storageUnitOfWork = storageUnitOfWork ?? throw new ArgumentNullException(nameof(storageUnitOfWork));
        _cancellationService = cancellationService ?? throw new ArgumentNullException(nameof(cancellationService));
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
        var cancelled = new List<(TaskExecution Task, TaskExecutionAttempt Attempt, TaskDispatch Dispatch)>();
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
                    if (dispatch is null || !IsPending(dispatch.DispatchStatus))
                    {
                        continue;
                    }

                    var previousDispatchStatus = dispatch.DispatchStatus;
                    dispatch.DispatchStatus = "Cancelled";
                    dispatch.FailedOnUtc = now;
                    dispatch.FailureReason = normalizedReason;
                    dispatch.Metadata[CancellationReasonMetadataKey] = JsonValue.Create(normalizedReason);
                    dispatch.Metadata[CancellationRequestedOnUtcMetadataKey] = JsonValue.Create(now);

                    if (attempt.Status is TaskExecutionStatus.Running or TaskExecutionStatus.Retrying)
                    {
                        attempt.Status = TaskExecutionStatus.Cancelled;
                        attempt.FailedOnUtc = now;
                        attempt.ErrorCode = "DispatchCancelled";
                        attempt.ErrorMessage = normalizedReason;
                    }

                    if (task.Status is TaskExecutionStatus.Running or TaskExecutionStatus.Retrying)
                    {
                        task.Status = TaskExecutionStatus.Cancelled;
                        task.FailedOnUtc = now;
                        task.WaitingSinceUtc = null;
                    }

                    await _dispatchRepository.Update(dispatch, cancellationToken);
                    await _attemptRepository.Update(attempt, cancellationToken);
                    await _taskRepository.Update(task, cancellationToken);
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
                    cancelled.Add((task, attempt, dispatch));
                }
            }

            await _storageUnitOfWork.SaveChanges(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        foreach (var (_, _, dispatch) in cancelled)
        {
            var result = await RequestPhysicalCancellationAsync(dispatch, normalizedReason, cancellationToken);
            dispatch.Metadata[CancellationProviderStatusMetadataKey] = JsonValue.Create(result.Status);
            dispatch.Metadata[CancellationProviderMessageMetadataKey] = JsonValue.Create(result.Message);
            await _dispatchRepository.Update(dispatch, cancellationToken);
        }

        return cancelled.Count;
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
        catch (Exception exception)
        {
            return TaskDispatchCancellationResult.LogicalOnly("ProviderError", exception.Message);
        }
    }

    private static bool IsPending(string dispatchStatus)
        => string.Equals(dispatchStatus, "Enqueued", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(dispatchStatus, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(dispatchStatus, "RetryPending", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(dispatchStatus, "ReplayPending", StringComparison.OrdinalIgnoreCase);
}
