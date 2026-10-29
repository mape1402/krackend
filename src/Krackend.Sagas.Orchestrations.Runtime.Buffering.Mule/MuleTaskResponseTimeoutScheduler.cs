using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mule;
using Mule.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Mule-backed response timeout scheduler. Timers are durable actions rather than a periodic scan over every waiting
/// saga. The task retains the action reference so callbacks can cancel it promptly.
/// </summary>
internal sealed class MuleTaskResponseTimeoutScheduler : ITaskResponseTimeoutScheduler
{
    private readonly IMuleStorage _storage;
    private readonly IMuleSerializer _serializer;
    private readonly IMuleCommitNotifier _commitNotifier;
    private readonly IMuleCancellationService _cancellation;
    private readonly ITaskExecutionRepository _tasks;
    private readonly ITaskExecutionAttemptRepository _attempts;
    private readonly ITaskDispatchRepository _dispatches;
    private readonly IOrchestrationInstanceCoordinator _coordinator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MuleTaskResponseTimeoutScheduler> _logger;

    public MuleTaskResponseTimeoutScheduler(
        IMuleStorage storage,
        IMuleSerializer serializer,
        IMuleCommitNotifier commitNotifier,
        IMuleCancellationService cancellation,
        ITaskExecutionRepository tasks,
        ITaskExecutionAttemptRepository attempts,
        ITaskDispatchRepository dispatches,
        IOrchestrationInstanceCoordinator coordinator,
        IServiceScopeFactory scopeFactory,
        ILogger<MuleTaskResponseTimeoutScheduler> logger)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _commitNotifier = commitNotifier ?? throw new ArgumentNullException(nameof(commitNotifier));
        _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
        _dispatches = dispatches ?? throw new ArgumentNullException(nameof(dispatches));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool UsesDurableScheduling => true;

    public async Task<TaskResponseTimeoutScheduleResult> ScheduleAsync(
        TaskResponseTimeoutScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new TaskResponseTimeoutActionRequest
        {
            OrchestrationInstanceId = request.OrchestrationInstanceId.ToString(),
            TaskExecutionId = request.TaskExecutionId.ToString(),
            TaskExecutionAttemptId = request.TaskExecutionAttemptId.ToString(),
            DispatchId = request.DispatchId.ToString(),
            AttemptNumber = request.AttemptNumber,
            DueOnUtc = request.DueOnUtc
        };
        var action = new DurableAction
        {
            Id = global::Mule.Ulid.NewUlid(),
            Key = MuleActionKeys.TaskResponseTimeoutActionKey,
            Lane = MuleActionKeys.TimeoutLane,
            Payload = _serializer.Serialize(payload),
            PayloadType = typeof(TaskResponseTimeoutActionRequest).AssemblyQualifiedName!,
            CorrelationId = request.OrchestrationInstanceId.ToString(),
            DeduplicationKey = BuildDeduplicationKey(request),
            Status = DurableActionStatus.Pending,
            CreatedOnUtc = DateTimeOffset.UtcNow,
            NextAttemptOnUtc = new DateTimeOffset(request.DueOnUtc, TimeSpan.Zero)
        };

        await _storage.AddAsync(action, cancellationToken);
        await _storage.SaveChangesAsync(cancellationToken);
        var actionId = await _storage.FindByDeduplicationKeyAsync(action.Key, action.DeduplicationKey, cancellationToken) ?? action.Id;

        var referenceCapture = await CaptureReferenceAsync(request, actionId, cancellationToken);
        if (referenceCapture == TimeoutReferenceCaptureResult.NoLongerAwaitingResponse)
        {
            // A callback may complete the task between persisting this durable action and acquiring the saga lease
            // to retain its reference. Do not leave the now-unreachable timer queued until its due date.
            await CancelPersistedActionAsync(actionId, request.TaskExecutionId, cancellationToken);
            return new TaskResponseTimeoutScheduleResult(actionId.ToString(), action.Lane);
        }

        await _commitNotifier.NotifySavedAsync(actionId, action.Lane, cancellationToken);
        NotifyWhenDue(actionId, action.Lane, action.NextAttemptOnUtc ?? DateTimeOffset.UtcNow);
        return new TaskResponseTimeoutScheduleResult(actionId.ToString(), action.Lane);
    }

    public async Task CancelAsync(TaskResponseTimeoutCancellationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        global::Mule.Ulid? actionId = null;
        if (!string.IsNullOrWhiteSpace(request.ActionId) && global::Mule.Ulid.TryParse(request.ActionId, out var capturedActionId))
        {
            actionId = capturedActionId;
        }

        if (actionId is null &&
            request.TaskExecutionAttemptId is { } attemptId &&
            request.AttemptNumber is { } attemptNumber &&
            request.DueOnUtc is { } dueOnUtc)
        {
            actionId = await _storage.FindByDeduplicationKeyAsync(
                MuleActionKeys.TaskResponseTimeoutActionKey,
                BuildDeduplicationKey(request.TaskExecutionId, attemptId, attemptNumber, dueOnUtc),
                cancellationToken);
        }

        if (actionId is { } persistedActionId)
        {
            await CancelPersistedActionAsync(persistedActionId, request.TaskExecutionId, cancellationToken);
        }
    }

    public async Task<int> RepairAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        var repaired = 0;
        var missing = await _tasks.GetResponseTimeoutsMissingAction(Math.Max(1, take), cancellationToken);
        foreach (var task in missing)
        {
            try
            {
                var attempt = (await _attempts.GetByTaskExecutionId(task.Id, cancellationToken))
                    .OrderByDescending(x => x.AttemptNumber)
                    .FirstOrDefault(x => x.Status == TaskExecutionStatus.WaitingResponse && x.DispatchId.HasValue);
                if (attempt?.DispatchId is not { } dispatchId || task.ResponseDeadlineUtc is not { } dueOnUtc)
                {
                    continue;
                }

                var dispatch = await _dispatches.TryGetById(dispatchId, cancellationToken);
                if (dispatch is null || !string.Equals(dispatch.DispatchStatus, "WaitingResponse", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await ScheduleAsync(new TaskResponseTimeoutScheduleRequest
                {
                    OrchestrationInstanceId = task.OrchestrationInstanceId,
                    TaskExecutionId = task.Id,
                    TaskExecutionAttemptId = attempt.Id,
                    DispatchId = dispatch.Id,
                    AttemptNumber = attempt.AttemptNumber,
                    DueOnUtc = dueOnUtc
                }, cancellationToken);
                repaired++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not repair response timeout action for task '{TaskId}'.", task.Id);
            }
        }

        return repaired;
    }

    private async Task<TimeoutReferenceCaptureResult> CaptureReferenceAsync(
        TaskResponseTimeoutScheduleRequest request,
        global::Mule.Ulid actionId,
        CancellationToken cancellationToken)
    {
        var result = TimeoutReferenceCaptureResult.Deferred;
        var executed = await _coordinator.TryExecuteAsync(request.OrchestrationInstanceId, async token =>
        {
            var task = await _tasks.GetById(request.TaskExecutionId, token);
            if (task.Status != TaskExecutionStatus.WaitingResponse || task.ResponseDeadlineUtc != request.DueOnUtc)
            {
                result = TimeoutReferenceCaptureResult.NoLongerAwaitingResponse;
                return;
            }

            task.TimeoutActionId = actionId.ToString();
            task.TimeoutActionLane = MuleActionKeys.TimeoutLane;
            await _tasks.Update(task, token);
            result = TimeoutReferenceCaptureResult.Captured;
        }, cancellationToken);

        return executed ? result : TimeoutReferenceCaptureResult.Deferred;
    }

    private async Task CancelPersistedActionAsync(
        global::Mule.Ulid actionId,
        Id taskExecutionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _cancellation.CancelAsync(actionId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The state transition has already been persisted. A claimed timeout rechecks state under the saga lease,
            // so a transient Mule failure must not undo valid business work or create a duplicate dispatch.
            _logger.LogWarning(exception, "Could not cancel task response timeout action '{ActionId}' for task '{TaskId}'.", actionId, taskExecutionId);
        }
    }

    private void NotifyWhenDue(global::Mule.Ulid actionId, string lane, DateTimeOffset dueOnUtc)
    {
        var delay = dueOnUtc - DateTimeOffset.UtcNow;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

        // Immediate dispatch deliberately does not poll Mule's durable table for future actions. Keep one lightweight
        // local wake-up for this durable timer; process restart remains safe because Mule's scheduled recovery reads
        // the persisted next due action on startup. This is a per-timeout timer, never a scan over waiting sagas.
        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > TimeSpan.Zero) await Task.Delay(delay);
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IMuleCommitNotifier>()
                    .NotifySavedAsync(actionId, lane, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not notify Mule about due response timeout '{ActionId}'.", actionId);
            }
        });
    }

    private static string BuildDeduplicationKey(TaskResponseTimeoutScheduleRequest request)
        => BuildDeduplicationKey(
            request.TaskExecutionId,
            request.TaskExecutionAttemptId,
            request.AttemptNumber,
            request.DueOnUtc);

    private static string BuildDeduplicationKey(
        Id taskExecutionId,
        Id taskExecutionAttemptId,
        int attemptNumber,
        DateTime dueOnUtc)
        => string.Join(':', "task-timeout", taskExecutionId, taskExecutionAttemptId, attemptNumber, dueOnUtc.Ticks);

    private enum TimeoutReferenceCaptureResult
    {
        Deferred,
        Captured,
        NoLongerAwaitingResponse
    }
}
