using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.HotState;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class StageExecutionRepository : RuntimeRepositoryBase, IStageExecutionRepository,
    IOrchestrationExecutionGraphReader, IRuntimeStageSummaryReader
{
    private readonly IRuntimeHotStateStore _hotState;

    public StageExecutionRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeHotStateStore hotState = null)
        : base(dbContext, unitOfWork)
    {
        _hotState = hotState;
    }

    public async Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default)
    {
        DbContext.StageExecutions.Add(stageExecution);
        await SaveChanges(cancellationToken);
    }

    public async Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default)
    {
        if (DbContext.Entry(stageExecution).State == EntityState.Detached)
        {
            DetachLocalTrackedEntity(DbContext.StageExecutions, stageExecution);
            DbContext.StageExecutions.Update(stageExecution);
            var entry = DbContext.Entry(stageExecution);
            entry.Property(x => x.OrchestrationInstanceId).IsModified = false;
            entry.Property(x => x.StageKey).IsModified = false;
            entry.Property(x => x.Order).IsModified = false;
            entry.Property(x => x.StartedOnUtc).IsModified = false;
            entry.Property(x => x.ParallelGroupCount).IsModified = false;
        }
        await SaveChanges(cancellationToken);
    }

    public async Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default)
    {
        if (_hotState?.IsEnabled == true)
        {
            var hot = RuntimeHotStateEntityCodec.Deserialize<StageExecution>(
                await _hotState.TryReadAsync(typeof(StageExecution).FullName!, stageExecutionId.ToString(), cancellationToken));
            if (hot is not null) return hot;
        }
        var durable = await DbContext.StageExecutions.FindAsync([stageExecutionId], cancellationToken)
            ?? throw new KeyNotFoundException($"Stage execution '{stageExecutionId}' was not found.");
        await Cache(durable, cancellationToken);
        return durable;
    }

    public async Task<StageExecution> GetByInstanceAndKey(Id instanceId, string stageKey, CancellationToken cancellationToken = default)
    {
        if (_hotState?.IsEnabled == true)
        {
            var hot = RuntimeHotStateEntityCodec.Deserialize<StageExecution>(
                await _hotState.TryReadByIndexAsync(typeof(StageExecution).FullName!, "instance-stage", $"{instanceId}|{stageKey}", cancellationToken));
            if (hot is not null) return hot;
        }
        var durable = await DbContext.StageExecutions.FirstOrDefaultAsync(
            x => x.OrchestrationInstanceId == instanceId && x.StageKey == stageKey, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage execution '{stageKey}' was not found for instance '{instanceId}'.");
        await Cache(durable, cancellationToken);
        return durable;
    }

    public async Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
    {
        var durable = await DbContext.StageExecutions
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .OrderBy(x => x.Order)
            .ToArrayAsync(cancellationToken);
        if (_hotState?.IsEnabled != true) return durable;
        var hot = await ReadHot(instanceId, cancellationToken);
        return durable.Concat(hot).GroupBy(item => item.Id).Select(group => group.Last())
            .OrderBy(item => item.Order).ToArray();
    }

    public async Task<OrchestrationExecutionGraph> GetExecutionGraph(
        Id instanceId,
        Id stageExecutionId,
        CancellationToken cancellationToken = default)
    {
        // One left join replaces the two sequential round trips previously required
        // by callback hydration. Only tasks from the active stage participate, while
        // every stage remains available for next-stage selection.
        if (_hotState?.IsEnabled == true)
        {
            var logicalStages = await GetByInstanceId(instanceId, cancellationToken);
            var taskRecords = await _hotState.ReadByInstanceAsync(typeof(TaskExecution).FullName!, instanceId, cancellationToken);
            var hotTasks = taskRecords.Select(RuntimeHotStateEntityCodec.Deserialize<TaskExecution>)
                .Where(item => item is not null && item.StageExecutionId == stageExecutionId)
                .Cast<TaskExecution>().ToArray();
            var durableTasks = await DbContext.TaskExecutions.Where(item =>
                item.OrchestrationInstanceId == instanceId && item.StageExecutionId == stageExecutionId)
                .ToArrayAsync(cancellationToken);
            return new OrchestrationExecutionGraph(logicalStages, durableTasks.Concat(hotTasks)
                .GroupBy(item => item.Id).Select(group => group.Last()).ToArray());
        }
        var rows = await (
            from stage in DbContext.StageExecutions
            join task in DbContext.TaskExecutions.Where(task => task.StageExecutionId == stageExecutionId)
                on stage.Id equals task.StageExecutionId into stageTasks
            from task in stageTasks.DefaultIfEmpty()
            where stage.OrchestrationInstanceId == instanceId
            orderby stage.Order
            select new { Stage = stage, Task = task })
            .ToArrayAsync(cancellationToken);

        var stages = rows
            .GroupBy(row => row.Stage.Id)
            .Select(group => group.First().Stage)
            .ToArray();
        var tasks = rows
            .Where(row => row.Task is not null)
            .Select(row => row.Task)
            .ToArray();
        return new OrchestrationExecutionGraph(stages, tasks);
    }

    public async Task<RuntimeStageSummaryPage> GetStageSummaries(Id instanceId, int take = 20, CancellationToken cancellationToken = default)
    {
        var size = Math.Clamp(take, 1, 50);
        if (_hotState?.IsEnabled == true)
        {
            var stages = (await GetByInstanceId(instanceId, cancellationToken)).OrderBy(x => x.Order).ThenBy(x => x.Id).Take(size + 1).ToArray();
            return new RuntimeStageSummaryPage(stages.Take(size).Select(x => new RuntimeStageSummary(x.StageKey, x.Order,
                x.Status.ToString(), x.WasSkipped, x.StartedOnUtc, x.CompletedOnUtc, x.FailedOnUtc)).ToArray(), stages.Length > size);
        }
        var rows = await DbContext.StageExecutions.AsNoTracking().Where(x => x.OrchestrationInstanceId == instanceId)
            .OrderBy(x => x.Order).ThenBy(x => x.Id).Take(size + 1)
            .Select(x => new { x.StageKey, x.Order, x.Status, x.WasSkipped, x.StartedOnUtc, x.CompletedOnUtc, x.FailedOnUtc })
            .ToArrayAsync(cancellationToken);
        return new RuntimeStageSummaryPage(rows.Take(size).Select(x => new RuntimeStageSummary(x.StageKey, x.Order,
            x.Status.ToString(), x.WasSkipped, x.StartedOnUtc, x.CompletedOnUtc, x.FailedOnUtc)).ToArray(), rows.Length > size);
    }

    private async Task<StageExecution[]> ReadHot(Id instanceId, CancellationToken cancellationToken)
    {
        var records = await _hotState!.ReadByInstanceAsync(typeof(StageExecution).FullName!, instanceId, cancellationToken);
        return records.Select(RuntimeHotStateEntityCodec.Deserialize<StageExecution>)
            .Where(item => item is not null).Cast<StageExecution>().ToArray();
    }

    private Task Cache(StageExecution entity, CancellationToken cancellationToken)
        => _hotState?.IsEnabled == true
            ? _hotState.CacheAsync(RuntimeHotStateEntityCodec.CreateCacheMutation(entity), cancellationToken)
            : Task.CompletedTask;
}
