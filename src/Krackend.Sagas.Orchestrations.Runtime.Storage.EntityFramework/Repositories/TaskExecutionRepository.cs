using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.HotState;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class TaskExecutionRepository : RuntimeRepositoryBase, ITaskExecutionRepository, IResponseTimeoutRepairPageReader, ITaskExecutionStageReader, ITaskResponseTimeoutReferenceRepository
{
    private readonly IRuntimeHotStateStore _hotState;

    public async Task<IReadOnlyCollection<TaskExecution>> GetByStageExecutionId(Id stageExecutionId, CancellationToken cancellationToken = default)
    {
        var durable = await DbContext.TaskExecutions.Where(x => x.StageExecutionId == stageExecutionId).ToArrayAsync(cancellationToken);
        if (_hotState?.IsEnabled != true) return durable;
        var instanceId = durable.FirstOrDefault()?.OrchestrationInstanceId;
        if (instanceId is null)
        {
            var stage = RuntimeHotStateEntityCodec.Deserialize<StageExecution>(
                await _hotState.TryReadAsync(typeof(StageExecution).FullName!, stageExecutionId.ToString(), cancellationToken));
            instanceId = stage?.OrchestrationInstanceId;
        }
        if (instanceId is null) return durable;
        var hot = await ReadHot(instanceId.Value, cancellationToken);
        return durable.Concat(hot.Where(item => item.StageExecutionId == stageExecutionId))
            .GroupBy(item => item.Id).Select(group => group.Last()).ToArray();
    }

    public TaskExecutionRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeHotStateStore hotState = null)
        : base(dbContext, unitOfWork)
    {
        _hotState = hotState;
    }

    public async Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default)
    {
        DbContext.TaskExecutions.Add(taskExecution);
        await SaveChanges(cancellationToken);
    }

    public async Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default)
    {
        if (DbContext.Entry(taskExecution).State == EntityState.Detached)
        {
            DetachLocalTrackedEntity(DbContext.TaskExecutions, taskExecution);
            DbContext.TaskExecutions.Update(taskExecution);
            var entry = DbContext.Entry(taskExecution);
            entry.Property(x => x.OrchestrationInstanceId).IsModified = false;
            entry.Property(x => x.StageExecutionId).IsModified = false;
            entry.Property(x => x.TaskKey).IsModified = false;
            entry.Property(x => x.TaskKind).IsModified = false;
            entry.Property(x => x.ExecutionMode).IsModified = false;
            entry.Property(x => x.ParallelGroupId).IsModified = false;
            entry.Property(x => x.OnErrorPolicy).IsModified = false;
            entry.Property(x => x.StartedOnUtc).IsModified = false;
            entry.Property(x => x.CorrelationId).IsModified = false;
        }
        await SaveChanges(cancellationToken);
    }

    public async Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default)
    {
        if (_hotState?.IsEnabled == true)
        {
            var hot = RuntimeHotStateEntityCodec.Deserialize<TaskExecution>(
                await _hotState.TryReadAsync(typeof(TaskExecution).FullName!, taskExecutionId.ToString(), cancellationToken));
            if (hot is not null) return hot;
        }
        var durable = await DbContext.TaskExecutions.FindAsync([taskExecutionId], cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution '{taskExecutionId}' was not found.");
        await Cache(durable, cancellationToken);
        return durable;
    }

    public async Task<bool> TryCaptureTimeoutAction(
        Id taskExecutionId,
        DateTime responseDeadlineUtc,
        string actionId,
        string lane,
        CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions
            .Where(x => x.Id == taskExecutionId &&
                x.Status == TaskExecutionStatus.WaitingResponse &&
                x.ResponseDeadlineUtc == responseDeadlineUtc)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.TimeoutActionId, actionId)
                .SetProperty(x => x.TimeoutActionLane, lane), cancellationToken) == 1;

    public async Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default)
    {
        if (_hotState?.IsEnabled == true)
        {
            var hot = RuntimeHotStateEntityCodec.Deserialize<TaskExecution>(
                await _hotState.TryReadByIndexAsync(typeof(TaskExecution).FullName!, "correlation", correlationId, cancellationToken));
            if (hot is not null) return hot;
        }
        var durable = await DbContext.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution with correlation id '{correlationId}' was not found.");
        await Cache(durable, cancellationToken);
        return durable;
    }

    public async Task<TaskExecution> GetByStageAndKey(Id stageExecutionId, string taskKey, CancellationToken cancellationToken = default)
    {
        if (_hotState?.IsEnabled == true)
        {
            var hot = RuntimeHotStateEntityCodec.Deserialize<TaskExecution>(
                await _hotState.TryReadByIndexAsync(typeof(TaskExecution).FullName!, "stage-task", $"{stageExecutionId}|{taskKey}", cancellationToken));
            if (hot is not null) return hot;
        }
        var durable = await DbContext.TaskExecutions.FirstOrDefaultAsync(
            x => x.StageExecutionId == stageExecutionId && x.TaskKey == taskKey, cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution '{taskKey}' was not found for stage '{stageExecutionId}'.");
        await Cache(durable, cancellationToken);
        return durable;
    }

    public async Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
    {
        var durable = await DbContext.TaskExecutions
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .ToArrayAsync(cancellationToken);
        if (_hotState?.IsEnabled != true) return durable;
        var hot = await ReadHot(instanceId, cancellationToken);
        return durable.Concat(hot).GroupBy(item => item.Id).Select(group => group.Last()).ToArray();
    }

    public async Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse && x.WaitingSinceUtc <= dueBeforeUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TaskExecution>> GetResponseTimeoutsDue(
        DateTime dueBeforeUtc,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Max(1, take);
        // Query projected deadlines first using the status/deadline index. Legacy waits without a deadline
        // may have no timeout policy and must not repeatedly fill the page ahead of expired deadlines.
        var due = await DbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                x.ResponseDeadlineUtc != null && x.ResponseDeadlineUtc <= dueBeforeUtc)
            .OrderBy(x => x.ResponseDeadlineUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        if (due.Length == limit) return due;

        var legacy = await DbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                x.ResponseDeadlineUtc == null && x.WaitingSinceUtc <= dueBeforeUtc)
            .OrderBy(x => x.WaitingSinceUtc)
            .Take(limit - due.Length)
            .ToArrayAsync(cancellationToken);
        return due.Concat(legacy).ToArray();
    }

    public async Task<IReadOnlyCollection<TaskExecution>> GetResponseTimeoutsMissingAction(
        int take = 100,
        CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                x.ResponseDeadlineUtc != null &&
                string.IsNullOrWhiteSpace(x.TimeoutActionId))
            .OrderBy(x => x.ResponseDeadlineUtc)
            .Take(Math.Max(1, take))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TaskExecution>> GetResponseTimeoutsDuePage(
        DateTime dueBeforeUtc, int skip, int take, CancellationToken cancellationToken = default)
    {
        var offset = Math.Max(0, skip);
        var limit = Math.Max(1, take);
        var projected = DbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                x.ResponseDeadlineUtc != null && x.ResponseDeadlineUtc <= dueBeforeUtc);
        var due = await projected.OrderBy(x => x.ResponseDeadlineUtc).ThenBy(x => x.Id)
            .Skip(offset).Take(limit).ToArrayAsync(cancellationToken);
        if (due.Length == limit) return due;

        var legacyOffset = offset == 0 ? 0 : Math.Max(0, offset - await projected.CountAsync(cancellationToken));
        var legacy = await DbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                x.ResponseDeadlineUtc == null && x.WaitingSinceUtc <= dueBeforeUtc)
            .OrderBy(x => x.WaitingSinceUtc).ThenBy(x => x.Id)
            .Skip(legacyOffset).Take(limit - due.Length).ToArrayAsync(cancellationToken);
        return due.Concat(legacy).ToArray();
    }

    public async Task<IReadOnlyCollection<TaskExecution>> GetResponseTimeoutsMissingActionPage(
        DateTime dueOnOrBeforeUtc,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                x.ResponseDeadlineUtc != null &&
                x.ResponseDeadlineUtc <= dueOnOrBeforeUtc &&
                string.IsNullOrWhiteSpace(x.TimeoutActionId))
            .OrderBy(x => x.ResponseDeadlineUtc).ThenBy(x => x.Id)
            .Skip(Math.Max(0, skip)).Take(Math.Max(1, take)).ToArrayAsync(cancellationToken);

    private async Task<TaskExecution[]> ReadHot(Id instanceId, CancellationToken cancellationToken)
    {
        var records = await _hotState!.ReadByInstanceAsync(typeof(TaskExecution).FullName!, instanceId, cancellationToken);
        return records.Select(RuntimeHotStateEntityCodec.Deserialize<TaskExecution>)
            .Where(item => item is not null).Cast<TaskExecution>().ToArray();
    }

    private Task Cache(TaskExecution entity, CancellationToken cancellationToken)
        => _hotState?.IsEnabled == true
            ? _hotState.CacheAsync(RuntimeHotStateEntityCodec.CreateCacheMutation(entity), cancellationToken)
            : Task.CompletedTask;
}
