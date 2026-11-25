using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.HotState;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class TaskDispatchRepository : RuntimeRepositoryBase, ITaskDispatchRepository, ITaskDispatchCancellationRepository, ITaskDispatchBatchReader, ITaskDispatchPublicationReceiptRepository, ITaskDispatchPublicationStateRepository, ITaskDispatchAwaitedPublicationRepository, ITaskDispatchActionReferenceRepository, ITaskDispatchCallbackReceiptRepository, ITaskDispatchExecutionStateReader, ITaskDispatchCallbackExecutionStateReader, ITaskDispatchIdentifiedCallbackExecutionStateReader, ITaskDispatchCachedExecutionStateReader, IEmbeddedTaskTransportStateRepository
{
    private readonly bool _persistSuccessfulTransportOutcomes;
    private readonly bool _embedCurrentTransportState;
    private readonly IRuntimeHotStateStore _hotState;
    private readonly RuntimeMutationLeaseScope _mutationLease;
    private readonly RuntimeEntityFrameworkProviderOptimizations? _providerOptimizations;

    public bool EmbedsCurrentTransportState => _embedCurrentTransportState;

    public TaskDispatchRepository(
        RuntimeDbContext dbContext,
        IRuntimeStorageUnitOfWork unitOfWork,
        IOptions<RuntimeEntityFrameworkStorageOptions> storageOptions = null,
        IRuntimeHotStateStore hotState = null,
        RuntimeMutationLeaseScope mutationLease = null)
        : base(dbContext, unitOfWork)
    {
        _persistSuccessfulTransportOutcomes = storageOptions?.Value.PersistSuccessfulTransportOutcomes ?? true;
        _embedCurrentTransportState = storageOptions?.Value.EmbedCurrentTransportState ?? false;
        _hotState = hotState;
        _mutationLease = mutationLease;
        _providerOptimizations = storageOptions?.Value.ProviderOptimizations;
    }

    public async Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default)
    {
        if (_embedCurrentTransportState && dispatch.Id == dispatch.TaskExecutionAttemptId)
        {
            var task = DbContext.TaskExecutions.Local.FirstOrDefault(x => x.Id == dispatch.TaskExecutionAttemptId)
                ?? throw new RuntimeStateConflictException(
                    $"Embedded dispatch '{dispatch.Id}' has no tracked task execution.");
            EmbeddedTaskTransportState.StoreDispatch(task, dispatch);
            await SaveChanges(cancellationToken);
            return;
        }

        DbContext.TaskDispatches.Add(dispatch);
        await SaveChanges(cancellationToken);
    }

    public async Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default)
    {
        if (!_persistSuccessfulTransportOutcomes &&
            string.Equals(dispatch.DispatchStatus, "Acknowledged", StringComparison.OrdinalIgnoreCase) &&
            !dispatch.CallbackPending &&
            string.IsNullOrWhiteSpace(dispatch.CallbackReceiptId))
        {
            // TaskExecution is the functional completion record. This transport row is
            // retained as the immutable publication intent; failure/recovery paths still
            // persist their exact outcome. Detach so SaveChanges cannot rediscover the
            // in-memory audit mutation through automatic change detection.
            DbContext.Entry(dispatch).State = EntityState.Detached;
            return;
        }

        if (_embedCurrentTransportState && dispatch.Id == dispatch.TaskExecutionAttemptId)
        {
            var task = DbContext.TaskExecutions.Local.FirstOrDefault(x => x.Id == dispatch.Id)
                ?? await DbContext.TaskExecutions.FirstOrDefaultAsync(x => x.Id == dispatch.Id, cancellationToken);
            if (task is not null && EmbeddedTaskTransportState.IsEmbedded(task))
            {
                EmbeddedTaskTransportState.StoreDispatch(task, dispatch);
                await SaveChanges(cancellationToken);
                return;
            }
        }

        var wasDetached = DbContext.Entry(dispatch).State == EntityState.Detached;
        if (wasDetached)
        {
            DetachLocalTrackedEntity(DbContext.TaskDispatches, dispatch);
            DbContext.TaskDispatches.Update(dispatch);
        }
        var entry = DbContext.Entry(dispatch);
        if (wasDetached)
        {
            entry.Property(x => x.CallbackReceiptId).IsModified = false;
            entry.Property(x => x.CallbackAcceptedOnUtc).IsModified = false;
            entry.Property(x => x.PendingCallbackPayload).IsModified = false;
            entry.Property(x => x.CallbackPending).IsModified = false;
            // A detached mutation loaded before publication must never erase its
            // independent receipt. Tracked hot-path mutations already have an exact
            // original-value snapshot and only write properties that actually changed.
            entry.Property(x => x.SentOnUtc).IsModified = dispatch.SentOnUtc.HasValue;
        }
        await SaveChanges(cancellationToken);
    }

    public async Task<TaskDispatch> TryAcceptCallback(Id dispatchId, string receiptId, string payload, DateTime acceptedOnUtc,
        CancellationToken cancellationToken = default, TaskCallbackIdentity identity = null)
    {
        if (_embedCurrentTransportState && _hotState?.IsEnabled == true)
        {
            var hotTask = await TryReadHotTask(dispatchId, cancellationToken);
            if (hotTask is not null &&
                EmbeddedTaskTransportState.TryReadAttempt(hotTask, out var hotAttempt) &&
                EmbeddedTaskTransportState.TryReadDispatch(hotTask, out var hotDispatch))
            {
                if (!await MatchesIdentity(hotTask, hotAttempt, identity, cancellationToken)) return null;
                if (hotDispatch.CallbackReceiptId is null && !IsFinished(hotDispatch.DispatchStatus))
                {
                    hotDispatch.CallbackReceiptId = receiptId;
                    hotDispatch.CallbackAcceptedOnUtc = acceptedOnUtc;
                    hotDispatch.PendingCallbackPayload = payload;
                    hotDispatch.CallbackPending = true;
                    EmbeddedTaskTransportState.StoreDispatch(hotTask, hotDispatch);
                    await PersistHotTask(hotTask, cancellationToken);
                }
                return hotDispatch;
            }
        }
        var matches = DbContext.TaskDispatches.Where(x => x.Id == dispatchId);
        if (identity is not null)
        {
            matches = matches.Where(dispatch => DbContext.TaskExecutionAttempts.Any(attempt =>
                attempt.Id == dispatch.TaskExecutionAttemptId &&
                (identity.AttemptNumber <= 0 || attempt.AttemptNumber == identity.AttemptNumber) &&
                (!identity.TaskExecutionId.HasValue || attempt.TaskExecutionId == identity.TaskExecutionId.Value) &&
                DbContext.TaskExecutions.Any(task => task.Id == attempt.TaskExecutionId &&
                    (!identity.InstanceId.HasValue || task.OrchestrationInstanceId == identity.InstanceId.Value) &&
                    (!identity.ArtifactId.HasValue || DbContext.OrchestrationInstances.Any(instance => instance.Id == task.OrchestrationInstanceId &&
                        instance.RuntimeOrchestrationArtifactId == identity.ArtifactId.Value)))));
        }
        await matches.Where(x => x.CallbackReceiptId == null &&
                x.DispatchStatus != "TimedOut" && x.DispatchStatus != "Cancelled" && x.DispatchStatus != "Completed" &&
                x.DispatchStatus != "Acknowledged" && x.DispatchStatus != "Failed")
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.CallbackReceiptId, receiptId)
                .SetProperty(x => x.CallbackAcceptedOnUtc, acceptedOnUtc)
                .SetProperty(x => x.PendingCallbackPayload, payload)
                .SetProperty(x => x.CallbackPending, true), cancellationToken);

        return await matches.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CompleteCallback(Id dispatchId, string receiptId, CancellationToken cancellationToken = default)
    {
        if (_embedCurrentTransportState && _hotState?.IsEnabled == true)
        {
            var hotTask = await TryReadHotTask(dispatchId, cancellationToken);
            if (hotTask is not null && EmbeddedTaskTransportState.TryReadDispatch(hotTask, out var hotDispatch) &&
                string.Equals(hotDispatch.CallbackReceiptId, receiptId, StringComparison.Ordinal) &&
                hotDispatch.CallbackPending)
            {
                hotDispatch.CallbackPending = false;
                hotDispatch.PendingCallbackPayload = null;
                EmbeddedTaskTransportState.StoreDispatch(hotTask, hotDispatch);
                await PersistHotTask(hotTask, cancellationToken);
                return;
            }
        }
        var tracked = DbContext.TaskDispatches.Local.FirstOrDefault(x =>
            x.Id == dispatchId &&
            string.Equals(x.CallbackReceiptId, receiptId, StringComparison.Ordinal) &&
            x.CallbackPending);
        if (tracked is not null)
        {
            // CompleteCallbackDecisionHandler has already attached this dispatch for
            // its functional update. Fold recovery-marker cleanup into that UPDATE;
            // do not add another SQL statement inside the lease transaction.
            tracked.CallbackPending = false;
            tracked.PendingCallbackPayload = null;
            var entry = DbContext.Entry(tracked);
            entry.Property(x => x.CallbackPending).IsModified = true;
            entry.Property(x => x.PendingCallbackPayload).IsModified = true;
            await SaveChanges(cancellationToken);
            return;
        }

        await DbContext.TaskDispatches
            .Where(x => x.Id == dispatchId && x.CallbackReceiptId == receiptId && x.CallbackPending)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.CallbackPending, false)
                .SetProperty(x => x.PendingCallbackPayload, (string)null), cancellationToken);
    }

    public async Task<IReadOnlyCollection<TaskDispatch>> GetPendingCallbacks(int take, int skip,
        CancellationToken cancellationToken = default)
        => await DbContext.TaskDispatches.AsNoTracking().Where(x => x.CallbackPending)
            .OrderBy(x => x.CallbackAcceptedOnUtc).ThenBy(x => x.Id).Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take)).ToArrayAsync(cancellationToken);

    public async Task MarkSent(Id dispatchId, string status, DateTime sentOnUtc, string externalReference = null, CancellationToken cancellationToken = default)
    {
        var dispatch = await DbContext.TaskDispatches.FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found.");

        dispatch.SentOnUtc = sentOnUtc;
        if (!IsFinished(dispatch.DispatchStatus))
        {
            dispatch.DispatchStatus = status;
        }

        await SaveChanges(cancellationToken);
    }

    public async Task RecordPublicationReceipt(Id dispatchId, DateTime publishedOnUtc, CancellationToken cancellationToken = default)
    {
        if (_embedCurrentTransportState)
        {
            if (_hotState is IRuntimeHotStateDispatchReceiptStore hotReceipts)
            {
                await hotReceipts.RecordDispatchPublicationAsync(
                    dispatchId, publishedOnUtc, cancellationToken);
                return;
            }
            if (_hotState?.IsEnabled == true)
            {
                var hotTask = await TryReadHotTask(dispatchId, cancellationToken);
                if (hotTask is not null && EmbeddedTaskTransportState.TryReadDispatch(hotTask, out var hotDispatch))
                {
                    if (hotDispatch.SentOnUtc is null)
                    {
                        hotDispatch.SentOnUtc = publishedOnUtc;
                        EmbeddedTaskTransportState.StoreDispatch(hotTask, hotDispatch);
                        await PersistHotTask(hotTask, cancellationToken);
                    }
                    return;
                }
            }
            var task = await DbContext.TaskExecutions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken);
            if (task is not null && EmbeddedTaskTransportState.TryReadDispatch(task, out var embedded))
            {
                if (embedded.SentOnUtc is null)
                {
                    embedded.SentOnUtc = publishedOnUtc;
                    EmbeddedTaskTransportState.StoreDispatch(task, embedded);
                    // Callback completion is stronger publication evidence and updates
                    // this same task row. Re-evaluate status at the UPDATE boundary so a
                    // callback that committed after our snapshot wins without a second
                    // metadata write or a same-task row-lock convoy.
                    await DbContext.TaskExecutions
                        .Where(x => x.Id == dispatchId &&
                            (x.Status == TaskExecutionStatus.Running ||
                             x.Status == TaskExecutionStatus.WaitingResponse ||
                             x.Status == TaskExecutionStatus.Retrying))
                        .ExecuteUpdateAsync(update => update.SetProperty(
                            x => x.Metadata, task.Metadata), cancellationToken);
                }
                return;
            }
        }
        await DbContext.TaskDispatches.Where(x => x.Id == dispatchId && x.SentOnUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.SentOnUtc, publishedOnUtc), cancellationToken);
    }

    public async Task<bool> TryConfirmPublication(
        Id dispatchId,
        string publishedStatus,
        DateTime publishedOnUtc,
        CancellationToken cancellationToken = default)
    {
        if (_embedCurrentTransportState)
        {
            if (_hotState is IRuntimeHotStateDispatchReceiptStore hotReceipts)
            {
                await hotReceipts.RecordDispatchPublicationAsync(
                    dispatchId, publishedOnUtc, cancellationToken);
                return true;
            }
            if (_hotState?.IsEnabled == true)
            {
                var hotTask = await TryReadHotTask(dispatchId, cancellationToken);
                if (hotTask is not null && EmbeddedTaskTransportState.TryReadDispatch(hotTask, out var hotDispatch))
                {
                    if (IsFinished(hotDispatch.DispatchStatus)) return false;
                    hotDispatch.SentOnUtc = publishedOnUtc;
                    hotDispatch.DispatchStatus = publishedStatus;
                    hotDispatch.FailedOnUtc = null;
                    hotDispatch.FailureReason = null;
                    EmbeddedTaskTransportState.StoreDispatch(hotTask, hotDispatch);
                    await PersistHotTask(hotTask, cancellationToken);
                    return true;
                }
            }
            var task = await DbContext.TaskExecutions.FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken);
            if (task is not null && EmbeddedTaskTransportState.TryReadDispatch(task, out var embedded))
            {
                if (IsFinished(embedded.DispatchStatus)) return false;
                embedded.SentOnUtc = publishedOnUtc;
                embedded.DispatchStatus = publishedStatus;
                embedded.FailedOnUtc = null;
                embedded.FailureReason = null;
                EmbeddedTaskTransportState.StoreDispatch(task, embedded);
                await SaveChanges(cancellationToken);
                return true;
            }
        }
        return await DbContext.TaskDispatches
            .Where(x => x.Id == dispatchId &&
                x.DispatchStatus != "Acknowledged" &&
                x.DispatchStatus != "Failed" &&
                x.DispatchStatus != "Completed" &&
                x.DispatchStatus != "TimedOut" &&
                x.DispatchStatus != "Cancelled")
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.SentOnUtc, publishedOnUtc)
                .SetProperty(x => x.DispatchStatus, publishedStatus)
                .SetProperty(x => x.FailedOnUtc, (DateTime?)null)
                .SetProperty(x => x.FailureReason, (string)null), cancellationToken) == 1;
    }

    public async Task<bool> TryConfirmAwaitedPublication(
        Id dispatchId,
        Id taskExecutionId,
        Id taskExecutionAttemptId,
        DateTime sentOnUtc,
        DateTime? responseDeadlineUtc,
        CancellationToken cancellationToken = default)
    {
        if (_embedCurrentTransportState && _hotState?.IsEnabled == true)
        {
            var hotTask = await TryReadHotTask(taskExecutionId, cancellationToken);
            if (hotTask is not null &&
                EmbeddedTaskTransportState.TryReadAttempt(hotTask, out var hotAttempt) &&
                EmbeddedTaskTransportState.TryReadDispatch(hotTask, out var hotDispatch) &&
                hotAttempt.Id == taskExecutionAttemptId && hotDispatch.Id == dispatchId)
            {
                if (IsFinished(hotDispatch.DispatchStatus)) return false;
                if (hotDispatch.DispatchStatus is not ("QueuePending" or "Enqueued" or "Scheduled" or "RetryPending" or "ReplayPending"))
                    throw new RuntimeStateConflictException(
                        $"Task dispatch '{dispatchId}' changed before awaited publication confirmation.");
                if (hotAttempt.Status != TaskExecutionStatus.Running || hotTask.Status != TaskExecutionStatus.Running)
                    throw new RuntimeStateConflictException(
                        $"Task graph for dispatch '{dispatchId}' changed before awaited publication confirmation.");

                hotDispatch.SentOnUtc = sentOnUtc;
                hotDispatch.DispatchStatus = "WaitingResponse";
                hotDispatch.FailedOnUtc = null;
                hotDispatch.FailureReason = null;
                hotAttempt.Status = TaskExecutionStatus.WaitingResponse;
                hotAttempt.WaitingSinceUtc = sentOnUtc;
                hotTask.Status = TaskExecutionStatus.WaitingResponse;
                hotTask.WaitingSinceUtc = sentOnUtc;
                hotTask.ResponseDeadlineUtc = responseDeadlineUtc;
                hotTask.TimeoutActionId = null;
                hotTask.TimeoutActionLane = null;
                EmbeddedTaskTransportState.StoreAttempt(hotTask, hotAttempt);
                EmbeddedTaskTransportState.StoreDispatch(hotTask, hotDispatch);
                await PersistHotTask(hotTask, cancellationToken);
                return true;
            }
        }
        var dispatchUpdated = await DbContext.TaskDispatches
            .Where(x => x.Id == dispatchId &&
                x.TaskExecutionAttemptId == taskExecutionAttemptId &&
                (x.DispatchStatus == "QueuePending" ||
                 x.DispatchStatus == "Enqueued" ||
                 x.DispatchStatus == "Scheduled" ||
                 x.DispatchStatus == "RetryPending" ||
                 x.DispatchStatus == "ReplayPending"))
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.SentOnUtc, sentOnUtc)
                .SetProperty(x => x.DispatchStatus, "WaitingResponse")
                .SetProperty(x => x.FailedOnUtc, (DateTime?)null)
                .SetProperty(x => x.FailureReason, (string)null), cancellationToken);
        if (dispatchUpdated == 0)
        {
            var status = await DbContext.TaskDispatches.AsNoTracking()
                .Where(x => x.Id == dispatchId)
                .Select(x => x.DispatchStatus)
                .FirstOrDefaultAsync(cancellationToken);
            if (IsFinished(status)) return false;
            throw new RuntimeStateConflictException(
                $"Task dispatch '{dispatchId}' changed before awaited publication confirmation.");
        }

        var attemptUpdated = await DbContext.TaskExecutionAttempts
            .Where(x => x.Id == taskExecutionAttemptId &&
                x.TaskExecutionId == taskExecutionId &&
                x.Status == TaskExecutionStatus.Running)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, TaskExecutionStatus.WaitingResponse)
                .SetProperty(x => x.WaitingSinceUtc, sentOnUtc), cancellationToken);
        var taskUpdated = await DbContext.TaskExecutions
            .Where(x => x.Id == taskExecutionId &&
                x.Status == TaskExecutionStatus.Running)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, TaskExecutionStatus.WaitingResponse)
                .SetProperty(x => x.WaitingSinceUtc, sentOnUtc)
                .SetProperty(x => x.ResponseDeadlineUtc, responseDeadlineUtc)
                .SetProperty(x => x.TimeoutActionId, (string)null)
                .SetProperty(x => x.TimeoutActionLane, (string)null), cancellationToken);
        if (attemptUpdated != 1 || taskUpdated != 1)
            throw new RuntimeStateConflictException(
                $"Task graph for dispatch '{dispatchId}' changed before awaited publication confirmation.");

        return true;
    }

    public async Task CaptureActionReference(
        Id dispatchId,
        string actionId,
        string lane,
        string acceptedStatus,
        CancellationToken cancellationToken = default)
    {
        var metadata = await DbContext.TaskDispatches.AsNoTracking()
            .Where(x => x.Id == dispatchId)
            .Select(x => x.Metadata)
            .FirstOrDefaultAsync(cancellationToken);
        if (metadata is null) return;

        metadata = metadata.ToDictionary(
            item => item.Key,
            item => item.Value?.DeepClone(),
            StringComparer.OrdinalIgnoreCase);
        metadata["Mule.ActionId"] = System.Text.Json.Nodes.JsonValue.Create(actionId);
        metadata["Mule.Lane"] = System.Text.Json.Nodes.JsonValue.Create(lane);
        await DbContext.TaskDispatches.Where(x => x.Id == dispatchId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Metadata, metadata)
                .SetProperty(x => x.DispatchStatus,
                    x => x.DispatchStatus == "QueuePending" ? acceptedStatus : x.DispatchStatus),
                cancellationToken);
    }

    public async Task MarkFailed(Id dispatchId, string failureReason, string externalReference = null, CancellationToken cancellationToken = default)
    {
        var dispatch = await TryGetById(dispatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found.");

        dispatch.DispatchStatus = "Failed";
        dispatch.FailedOnUtc = DateTime.UtcNow;
        dispatch.FailureReason = failureReason;
        await Update(dispatch, cancellationToken);
    }

    public async Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default)
        => await TryGetById(dispatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found.");

    public async Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default)
    {
        if (_embedCurrentTransportState && _hotState?.IsEnabled == true)
        {
            var hotTask = await TryReadHotTask(dispatchId, cancellationToken);
            if (hotTask is not null && EmbeddedTaskTransportState.TryReadDispatch(hotTask, out var hotDispatch))
                return hotDispatch;
        }
        // Added/modified dispatches are part of the current activation and must stay
        // visible before its final commit. An unchanged tracked entity, however, can
        // be stale after an atomic receipt/update from another scope. Read those as a
        // fresh snapshot; Update already knows how to replace a detached local copy.
        var local = DbContext.TaskDispatches.Local.FirstOrDefault(x => x.Id == dispatchId);
        if (local is not null && DbContext.Entry(local).State != EntityState.Unchanged)
            return local;

        var dispatch = await DbContext.TaskDispatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken);
        if (dispatch is not null) return dispatch;
        if (_embedCurrentTransportState)
        {
            var task = await DbContext.TaskExecutions.FindAsync([dispatchId], cancellationToken);
            if (task is not null && EmbeddedTaskTransportState.TryReadDispatch(task, out dispatch)) return dispatch;
        }
        return null;
    }

    public async Task<TaskDispatchExecutionState> GetExecutionState(
        Id dispatchId,
        CancellationToken cancellationToken = default)
    {
        if (_embedCurrentTransportState && _hotState?.IsEnabled == true)
        {
            var hotState = await TryReadHotExecutionState(dispatchId, cancellationToken);
            if (hotState is not null) return hotState;
        }
        var state = await (from dispatch in DbContext.TaskDispatches
                  join attempt in DbContext.TaskExecutionAttempts
                      on dispatch.TaskExecutionAttemptId equals attempt.Id
                  join task in DbContext.TaskExecutions
                      on attempt.TaskExecutionId equals task.Id
                  join instance in DbContext.OrchestrationInstances
                      on task.OrchestrationInstanceId equals instance.Id
                  join stage in DbContext.StageExecutions
                      on task.StageExecutionId equals stage.Id
                  where dispatch.Id == dispatchId
                  select new TaskDispatchExecutionState(instance, task, attempt, dispatch, stage))
            .FirstOrDefaultAsync(cancellationToken);
        if (state is not null) return state;
        if (_embedCurrentTransportState)
        {
            var task = await DbContext.TaskExecutions.FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken);
            if (task is not null &&
                EmbeddedTaskTransportState.TryReadAttempt(task, out var attempt) &&
                EmbeddedTaskTransportState.TryReadDispatch(task, out var dispatch))
            {
                var instance = await DbContext.OrchestrationInstances.FirstAsync(
                    x => x.Id == task.OrchestrationInstanceId, cancellationToken);
                var stage = await DbContext.StageExecutions.FirstAsync(
                    x => x.Id == task.StageExecutionId, cancellationToken);
                return new TaskDispatchExecutionState(instance, task, attempt, dispatch, stage);
            }
        }
        throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found.");
    }

    public async Task<TaskDispatchCallbackExecutionState> GetCallbackExecutionState(
        Id dispatchId,
        CancellationToken cancellationToken = default)
    {
        if (_embedCurrentTransportState && _hotState?.IsEnabled == true)
        {
            var execution = await TryReadHotExecutionState(dispatchId, cancellationToken);
            if (execution is not null)
            {
                var stageRecordsTask = _hotState.ReadByInstanceAsync(
                    typeof(StageExecution).FullName!, execution.Instance.Id, cancellationToken);
                var taskRecordsTask = _hotState.ReadByInstanceAsync(
                    typeof(TaskExecution).FullName!, execution.Instance.Id, cancellationToken);
                await Task.WhenAll(stageRecordsTask, taskRecordsTask);
                var stageRecords = await stageRecordsTask;
                var taskRecords = await taskRecordsTask;
                var hotStages = stageRecords.Select(RuntimeHotStateEntityCodec.Deserialize<StageExecution>)
                    .Where(item => item is not null).Cast<StageExecution>()
                    .OrderBy(item => item.Order).ToArray();
                var hotTasks = taskRecords.Select(RuntimeHotStateEntityCodec.Deserialize<TaskExecution>)
                    .Where(item => item is not null && item.StageExecutionId == execution.Task.StageExecutionId)
                    .Cast<TaskExecution>().ToArray();

                StageExecution[] logicalStages;
                TaskExecution[] logicalTasks;
                if (HasCompleteHotStagePrefix(hotStages))
                {
                    // A hot-created instance retains every materialized stage and task
                    // even after its journal batches project. Redis therefore owns the
                    // complete decision graph; querying SQL again here would put two
                    // database round trips back into every callback.
                    logicalStages = hotStages;
                    logicalTasks = hotTasks;
                }
                else
                {
                    // An instance first touched after hot state was enabled can have a
                    // partial Redis graph. Merge durable history only for that migration
                    // case, then subsequent mutations remain entirely on the hot path.
                    var durableStages = await DbContext.StageExecutions.AsNoTracking()
                        .Where(item => item.OrchestrationInstanceId == execution.Instance.Id)
                        .ToArrayAsync(cancellationToken);
                    var durableTasks = await DbContext.TaskExecutions.AsNoTracking()
                        .Where(item => item.OrchestrationInstanceId == execution.Instance.Id &&
                            item.StageExecutionId == execution.Task.StageExecutionId)
                        .ToArrayAsync(cancellationToken);
                    logicalStages = durableStages.Concat(hotStages).GroupBy(item => item.Id)
                        .Select(group => group.Last()).OrderBy(item => item.Order).ToArray();
                    logicalTasks = durableTasks.Concat(hotTasks).GroupBy(item => item.Id)
                        .Select(group => group.Last()).ToArray();
                }
                return new TaskDispatchCallbackExecutionState(
                    execution, new OrchestrationExecutionGraph(logicalStages, logicalTasks));
            }
        }
        if (_embedCurrentTransportState)
        {
            if (_providerOptimizations?.TryReadCallbackGraphAsync is { } readCallbackGraph)
            {
                var compactState = await readCallbackGraph(
                    DbContext,
                    dispatchId,
                    cancellationToken);
                if (compactState is not null)
                    return compactState;
            }
            else if (string.Equals(
                    DbContext.Database.ProviderName,
                    "Npgsql.EntityFrameworkCore.PostgreSQL",
                    StringComparison.Ordinal))
            {
                var compactState = await PostgreSqlCallbackGraphReader.TryReadAsync(
                    DbContext,
                    dispatchId,
                    cancellationToken);
                if (compactState is not null)
                    return compactState;
            }
            // Keep callback ownership and the decision graph as two compact result sets.
            // Joining the instance and callback task to every stage/task row repeats the
            // complete snapshot payload for every graph row. During callback bursts that
            // query became the dominant SQL Server statement and created latch pressure
            // even though unrelated instances were being processed concurrently.
            var embeddedOwnership = await (
                from task in DbContext.TaskExecutions
                join instance in DbContext.OrchestrationInstances
                    on task.OrchestrationInstanceId equals instance.Id
                join callbackStage in DbContext.StageExecutions
                    on task.StageExecutionId equals callbackStage.Id
                where task.Id == dispatchId
                select new
                {
                    Instance = instance,
                    Task = task,
                    CallbackStage = callbackStage
                }).FirstOrDefaultAsync(cancellationToken);

            if (embeddedOwnership is not null)
            {
                if (!EmbeddedTaskTransportState.TryReadAttempt(embeddedOwnership.Task, out var embeddedAttempt) ||
                    !EmbeddedTaskTransportState.TryReadDispatch(embeddedOwnership.Task, out var embeddedDispatch))
                    throw new RuntimeStateConflictException(
                        $"Task '{dispatchId}' is missing its embedded transport intent.");

                // This result has at most one row per stage plus the tasks belonging to
                // the callback stage. It never repeats the instance snapshot or callback
                // ownership chain, and remains tracked in the current mutation scope.
                var graphRows = await (
                    from graphStage in DbContext.StageExecutions
                    join graphTask in DbContext.TaskExecutions.Where(candidate =>
                            candidate.StageExecutionId == embeddedOwnership.Task.StageExecutionId)
                        on graphStage.Id equals graphTask.StageExecutionId into graphTasks
                    from graphTask in graphTasks.DefaultIfEmpty()
                    where graphStage.OrchestrationInstanceId == embeddedOwnership.Instance.Id
                    orderby graphStage.Order
                    select new { Stage = graphStage, Task = graphTask })
                    .ToArrayAsync(cancellationToken);

                var embeddedStages = graphRows.GroupBy(row => row.Stage.Id)
                    .Select(group => group.First().Stage).ToArray();
                var embeddedTasks = graphRows.Where(row => row.Task is not null)
                    .GroupBy(row => row.Task.Id).Select(group => group.First().Task).ToArray();
                return new TaskDispatchCallbackExecutionState(
                    new TaskDispatchExecutionState(embeddedOwnership.Instance, embeddedOwnership.Task, embeddedAttempt,
                        embeddedDispatch, embeddedOwnership.CallbackStage),
                    new OrchestrationExecutionGraph(embeddedStages, embeddedTasks));
            }
        }

        // Callback advancement needs the dispatch ownership chain, every instance stage and the
        // tasks in the callback stage. Materializing that graph in one query avoids a second SQL
        // round trip for every service response while preserving one consistent tracked snapshot.
        var rows = await (
            from dispatch in DbContext.TaskDispatches
            join attempt in DbContext.TaskExecutionAttempts
                on dispatch.TaskExecutionAttemptId equals attempt.Id
            join task in DbContext.TaskExecutions
                on attempt.TaskExecutionId equals task.Id
            join instance in DbContext.OrchestrationInstances
                on task.OrchestrationInstanceId equals instance.Id
            join callbackStage in DbContext.StageExecutions
                on task.StageExecutionId equals callbackStage.Id
            join graphStage in DbContext.StageExecutions
                on instance.Id equals graphStage.OrchestrationInstanceId
            join graphTask in DbContext.TaskExecutions
                on new
                {
                    GraphStageId = graphStage.Id,
                    CallbackStageId = task.StageExecutionId
                }
                equals new
                {
                    GraphStageId = graphTask.StageExecutionId,
                    CallbackStageId = graphTask.StageExecutionId
                } into graphTasks
            from graphTask in graphTasks.DefaultIfEmpty()
            where dispatch.Id == dispatchId
            select new
            {
                Instance = instance,
                Task = task,
                Attempt = attempt,
                Dispatch = dispatch,
                CallbackStage = callbackStage,
                GraphStage = graphStage,
                GraphTask = graphTask
            })
            .ToArrayAsync(cancellationToken);

        if (rows.Length == 0)
            throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found.");

        var ownership = rows[0];
        var stages = rows
            .GroupBy(row => row.GraphStage.Id)
            .Select(group => group.First().GraphStage)
            .OrderBy(stage => stage.Order)
            .ToArray();
        var stageTasks = rows
            .Where(row => row.GraphTask is not null &&
                row.GraphTask.StageExecutionId == ownership.Task.StageExecutionId)
            .GroupBy(row => row.GraphTask.Id)
            .Select(group => group.First().GraphTask)
            .ToArray();

        return new TaskDispatchCallbackExecutionState(
            new TaskDispatchExecutionState(
                ownership.Instance,
                ownership.Task,
                ownership.Attempt,
                ownership.Dispatch,
                ownership.CallbackStage),
            new OrchestrationExecutionGraph(stages, stageTasks));
    }

    public async Task<TaskDispatchCallbackExecutionState> GetCallbackExecutionState(
        Id instanceId,
        Id taskExecutionId,
        Id dispatchId,
        CancellationToken cancellationToken = default)
    {
        if (_embedCurrentTransportState &&
            _hotState is IRuntimeHotStateInstanceGraphStore graphStore)
        {
            var protectedGraph = _mutationLease?.CurrentGraph;
            var records = protectedGraph is not null &&
                _mutationLease!.Current?.InstanceId == instanceId
                    ? protectedGraph.Records
                    : await graphStore.ReadInstanceGraphAsync(instanceId, cancellationToken);
            if (records.Count > 0)
            {
                var instance = records
                    .Where(record => record.Operation != RuntimeHotStateOperation.Delete &&
                        string.Equals(record.EntityType, typeof(OrchestrationInstance).FullName!, StringComparison.Ordinal) &&
                        string.Equals(record.EntityId, instanceId.ToString(), StringComparison.Ordinal))
                    .Select(RuntimeHotStateEntityCodec.Deserialize<OrchestrationInstance>)
                    .FirstOrDefault(item => item is not null);
                var task = records
                    .Where(record => record.Operation != RuntimeHotStateOperation.Delete &&
                        string.Equals(record.EntityType, typeof(TaskExecution).FullName!, StringComparison.Ordinal) &&
                        string.Equals(record.EntityId, taskExecutionId.ToString(), StringComparison.Ordinal))
                    .Select(RuntimeHotStateEntityCodec.Deserialize<TaskExecution>)
                    .FirstOrDefault(item => item is not null);

                if (instance is not null && task is not null &&
                    task.OrchestrationInstanceId == instanceId &&
                    EmbeddedTaskTransportState.TryReadAttempt(task, out var attempt) &&
                    EmbeddedTaskTransportState.TryReadDispatch(task, out var dispatch) &&
                    dispatch.Id == dispatchId)
                {
                    var hotStages = records
                        .Where(record => record.Operation != RuntimeHotStateOperation.Delete &&
                            string.Equals(record.EntityType, typeof(StageExecution).FullName!, StringComparison.Ordinal))
                        .Select(RuntimeHotStateEntityCodec.Deserialize<StageExecution>)
                        .Where(item => item is not null)
                        .Cast<StageExecution>()
                        .OrderBy(item => item.Order)
                        .ToArray();
                    var callbackStage = hotStages.FirstOrDefault(item => item.Id == task.StageExecutionId);
                    if (callbackStage is not null && HasCompleteHotStagePrefix(hotStages))
                    {
                        var hotTasks = records
                            .Where(record => record.Operation != RuntimeHotStateOperation.Delete &&
                                string.Equals(record.EntityType, typeof(TaskExecution).FullName!, StringComparison.Ordinal))
                            .Select(RuntimeHotStateEntityCodec.Deserialize<TaskExecution>)
                            .Where(item => item is not null && item.StageExecutionId == task.StageExecutionId)
                            .Cast<TaskExecution>()
                            .ToArray();
                        return new TaskDispatchCallbackExecutionState(
                            new TaskDispatchExecutionState(instance, task, attempt, dispatch, callbackStage),
                            new OrchestrationExecutionGraph(hotStages, hotTasks));
                    }
                }
            }
        }

        // Instances created before this layout (or durable-only providers) keep the
        // existing migration/provider path without weakening callback identity checks.
        return await GetCallbackExecutionState(dispatchId, cancellationToken);
    }

    public bool TryReadCachedExecutionState(
        OrchestrationInstance instance,
        TaskExecution task,
        StageExecution stage,
        out TaskDispatchExecutionState executionState)
    {
        executionState = null;
        if (!_embedCurrentTransportState ||
            instance is null ||
            task is null ||
            stage is null)
        {
            return false;
        }

        // A timeout activation can load the aggregate through the normal tracked
        // mutation path while the recovery snapshot was read with AsNoTracking.
        // Reuse the lease-protected tracked entities when they already exist. An
        // Attach of the detached snapshot would otherwise throw for the duplicate
        // key and leave the task in WaitingResponse forever.
        instance = DbContext.OrchestrationInstances.Local.FirstOrDefault(x => x.Id == instance.Id) ?? instance;
        task = DbContext.TaskExecutions.Local.FirstOrDefault(x => x.Id == task.Id) ?? task;
        stage = DbContext.StageExecutions.Local.FirstOrDefault(x => x.Id == stage.Id) ?? stage;

        if (task.OrchestrationInstanceId != instance.Id ||
            task.StageExecutionId != stage.Id ||
            !EmbeddedTaskTransportState.TryReadAttempt(task, out var attempt) ||
            !EmbeddedTaskTransportState.TryReadDispatch(task, out var dispatch) ||
            attempt.TaskExecutionId != task.Id ||
            dispatch.TaskExecutionAttemptId != attempt.Id)
        {
            return false;
        }


        // These objects were detached only after their previous activation committed.
        // Attach the pre-mutation snapshot as Unchanged now, before callback handlers
        // modify it, so DetectChanges emits narrow property updates instead of marking
        // every column on the cached rows as modified.
        if (DbContext.Entry(instance).State == EntityState.Detached)
            DbContext.OrchestrationInstances.Attach(instance);
        if (DbContext.Entry(task).State == EntityState.Detached)
            DbContext.TaskExecutions.Attach(task);
        if (DbContext.Entry(stage).State == EntityState.Detached)
            DbContext.StageExecutions.Attach(stage);

        executionState = new TaskDispatchExecutionState(instance, task, attempt, dispatch, stage);
        return true;
    }

    public async Task<IReadOnlyCollection<TaskDispatch>> GetByIds(
        IReadOnlyCollection<Id> dispatchIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatchIds);
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<TaskDispatch>();
        foreach (var batch in dispatchIds.Distinct().Chunk(200))
        {
            results.AddRange(await DbContext.TaskDispatches.AsNoTracking()
                .Where(x => batch.Contains(x.Id)).ToArrayAsync(cancellationToken));
            if (_embedCurrentTransportState)
            {
                var tasks = await DbContext.TaskExecutions.AsNoTracking()
                    .Where(x => batch.Contains(x.Id)).ToArrayAsync(cancellationToken);
                results.AddRange(tasks.Select(task => EmbeddedTaskTransportState.TryReadDispatch(task, out var dispatch)
                        ? dispatch
                        : null)
                    .Where(dispatch => dispatch is not null));
            }
        }
        return results;
    }

    public async Task<IReadOnlyCollection<TaskDispatch>> GetByAttemptIds(
        IReadOnlyCollection<Id> attemptIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attemptIds);
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<TaskDispatch>();
        foreach (var batch in attemptIds.Distinct().Chunk(200))
            results.AddRange(await DbContext.TaskDispatches.AsNoTracking()
                .Where(x => batch.Contains(x.TaskExecutionAttemptId)).ToArrayAsync(cancellationToken));
        return results;
    }

    public async Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default)
    {
        var dispatch = await DbContext.TaskDispatches.FirstOrDefaultAsync(x => x.CommandId == commandId, cancellationToken);
        if (dispatch is not null) return dispatch;
        if (_embedCurrentTransportState)
        {
            var tasks = await DbContext.TaskExecutions.AsNoTracking()
                .Where(x => x.Status == TaskExecutionStatus.WaitingResponse || x.Status == TaskExecutionStatus.Running)
                .ToArrayAsync(cancellationToken);
            dispatch = tasks.Select(task => EmbeddedTaskTransportState.TryReadDispatch(task, out var candidate)
                    ? candidate
                    : null)
                .FirstOrDefault(candidate => string.Equals(candidate?.CommandId, commandId, StringComparison.Ordinal));
            if (dispatch is not null) return dispatch;
        }
        throw new KeyNotFoundException($"Task dispatch with command id '{commandId}' was not found.");
    }

    public async Task<TaskDispatch> GetByAttemptId(
        Id taskExecutionAttemptId,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await DbContext.TaskDispatches.FirstOrDefaultAsync(
            x => x.TaskExecutionAttemptId == taskExecutionAttemptId,
            cancellationToken);
        if (dispatch is not null)
            return dispatch;

        // Embedded transport state deliberately shares task, attempt and dispatch
        // identity. Retain that fast fallback without applying the same assumption to
        // ordinary relational dispatch rows whose own Id is independent.
        if (_embedCurrentTransportState)
        {
            dispatch = await TryGetById(taskExecutionAttemptId, cancellationToken);
            if (dispatch is not null)
                return dispatch;
        }

        throw new KeyNotFoundException(
            $"Task dispatch for attempt '{taskExecutionAttemptId}' was not found.");
    }

    public async Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
        => await DbContext.TaskDispatches.AsNoTracking()
            .Where(x => x.ScheduledOnUtc <= dueBeforeUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TaskDispatch>> GetQueuePending(int take, CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Max(1, take);
        var queued = await DbContext.TaskDispatches.AsNoTracking()
            .Where(x => x.DispatchStatus == "QueuePending")
            .OrderBy(x => x.ScheduledOnUtc)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        // Prepared direct dispatches do not pollute QueuePending. A command whose task
        // graph is waiting but has no publication receipt is still a durable recovery
        // candidate. Start from the narrow filtered waiting-task/attempt indexes and
        // reach the dispatch through their keys instead of indexing every healthy
        // TaskDispatch by status.
        var direct = await (from task in DbContext.TaskExecutions.AsNoTracking()
                            join attempt in DbContext.TaskExecutionAttempts.AsNoTracking()
                                on task.Id equals attempt.TaskExecutionId
                            join dispatch in DbContext.TaskDispatches.AsNoTracking()
                                on attempt.Id equals dispatch.TaskExecutionAttemptId
                            where task.Status == TaskExecutionStatus.WaitingResponse &&
                                  attempt.Status == TaskExecutionStatus.WaitingResponse &&
                                  dispatch.DispatchStatus == "WaitingResponse" &&
                                  dispatch.SentOnUtc == null
                            orderby dispatch.ScheduledOnUtc
                            select dispatch)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        var embedded = Array.Empty<TaskDispatch>();
        if (_embedCurrentTransportState)
        {
            var tasks = await DbContext.TaskExecutions.AsNoTracking()
                .Where(x => x.Status == TaskExecutionStatus.WaitingResponse)
                .OrderBy(x => x.ResponseDeadlineUtc)
                .Take(pageSize)
                .ToArrayAsync(cancellationToken);
            embedded = tasks.Select(task => EmbeddedTaskTransportState.TryReadDispatch(task, out var dispatch)
                    ? dispatch
                    : null)
                .Where(dispatch => dispatch is not null && dispatch.SentOnUtc is null &&
                    (string.Equals(dispatch.DispatchStatus, "WaitingResponse", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(dispatch.DispatchStatus, "QueuePending", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        return queued.Concat(direct).Concat(embedded)
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.ScheduledOnUtc)
            .Take(pageSize)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<TaskDispatch>> GetPhysicalCancellationPending(
        int take,
        int skip,
        DateTime attemptedBeforeUtc,
        CancellationToken cancellationToken = default)
        => await DbContext.TaskDispatches.AsNoTracking()
            .Where(x => x.PhysicalCancellationPending &&
                (x.PhysicalCancellationLastAttemptOnUtc == null || x.PhysicalCancellationLastAttemptOnUtc <= attemptedBeforeUtc))
            .OrderBy(x => x.PhysicalCancellationLastAttemptOnUtc)
            .ThenBy(x => x.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take))
            .ToArrayAsync(cancellationToken);

    private async Task<TaskExecution> TryReadHotTask(Id taskId, CancellationToken cancellationToken)
    {
        var tracked = DbContext.TaskExecutions.Local.FirstOrDefault(item => item.Id == taskId);
        if (tracked is not null && EmbeddedTaskTransportState.IsEmbedded(tracked)) return tracked;
        var task = RuntimeHotStateEntityCodec.Deserialize<TaskExecution>(
            await _hotState.TryReadAsync(typeof(TaskExecution).FullName!, taskId.ToString(), cancellationToken));
        if (task is not null &&
            _hotState is IRuntimeHotStateDispatchReceiptStore hotReceipts &&
            EmbeddedTaskTransportState.TryReadDispatch(task, out var dispatch) &&
            dispatch.SentOnUtc is null &&
            await hotReceipts.TryReadDispatchPublicationAsync(taskId, cancellationToken) is { } publishedOnUtc)
        {
            dispatch.SentOnUtc = publishedOnUtc;
            EmbeddedTaskTransportState.StoreDispatch(task, dispatch);
        }
        return task;
    }

    private async Task<TaskDispatchExecutionState> TryReadHotExecutionState(
        Id dispatchId,
        CancellationToken cancellationToken)
    {
        var task = await TryReadHotTask(dispatchId, cancellationToken)
            ?? await DbContext.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(
                item => item.Id == dispatchId, cancellationToken);
        if (task is null ||
            !EmbeddedTaskTransportState.TryReadAttempt(task, out var attempt) ||
            !EmbeddedTaskTransportState.TryReadDispatch(task, out var dispatch))
            return null;

        var instanceRecordTask = _hotState.TryReadAsync(typeof(OrchestrationInstance).FullName!,
            task.OrchestrationInstanceId.ToString(), cancellationToken);
        var stageRecordTask = _hotState.TryReadAsync(typeof(StageExecution).FullName!,
            task.StageExecutionId.ToString(), cancellationToken);
        await Task.WhenAll(instanceRecordTask, stageRecordTask);
        var instance = RuntimeHotStateEntityCodec.Deserialize<OrchestrationInstance>(await instanceRecordTask);
        var stage = RuntimeHotStateEntityCodec.Deserialize<StageExecution>(await stageRecordTask);
        instance ??= await DbContext.OrchestrationInstances.AsNoTracking().FirstOrDefaultAsync(
            item => item.Id == task.OrchestrationInstanceId, cancellationToken);
        stage ??= await DbContext.StageExecutions.AsNoTracking().FirstOrDefaultAsync(
            item => item.Id == task.StageExecutionId, cancellationToken);
        if (instance is null || stage is null) return null;
        return new TaskDispatchExecutionState(instance, task, attempt, dispatch, stage);
    }

    private static bool HasCompleteHotStagePrefix(IReadOnlyList<StageExecution> stages)
    {
        if (stages.Count == 0 || stages[0].Order != 1) return false;
        for (var index = 1; index < stages.Count; index++)
        {
            if (stages[index].Order != stages[index - 1].Order + 1)
                return false;
        }
        return true;
    }

    private async Task<bool> MatchesIdentity(
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskCallbackIdentity identity,
        CancellationToken cancellationToken)
    {
        if (identity is null) return true;
        if (identity.AttemptNumber > 0 && attempt.AttemptNumber != identity.AttemptNumber) return false;
        if (identity.TaskExecutionId.HasValue && task.Id != identity.TaskExecutionId.Value) return false;
        if (identity.InstanceId.HasValue && task.OrchestrationInstanceId != identity.InstanceId.Value) return false;
        if (!identity.ArtifactId.HasValue) return true;

        var instance = RuntimeHotStateEntityCodec.Deserialize<OrchestrationInstance>(
            await _hotState.TryReadAsync(typeof(OrchestrationInstance).FullName!,
                task.OrchestrationInstanceId.ToString(), cancellationToken))
            ?? await DbContext.OrchestrationInstances.AsNoTracking().FirstOrDefaultAsync(
                item => item.Id == task.OrchestrationInstanceId, cancellationToken);
        return instance?.RuntimeOrchestrationArtifactId == identity.ArtifactId.Value;
    }

    private async Task PersistHotTask(TaskExecution task, CancellationToken cancellationToken)
    {
        if (_mutationLease?.Current is not null)
        {
            if (DbContext.Entry(task).State == EntityState.Detached)
            {
                DetachLocalTrackedEntity(DbContext.TaskExecutions, task);
                DbContext.TaskExecutions.Update(task);
            }
            await SaveChanges(cancellationToken);
            return;
        }

        await _hotState.CommitAsync(
            [RuntimeHotStateEntityCodec.CreateCacheMutation(task)], cancellationToken);
    }

    private static bool IsFinished(string status)
        => string.Equals(status, "Acknowledged", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "TimedOut", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
}
