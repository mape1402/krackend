using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class TaskExecutionRepository : RuntimeRepositoryBase, ITaskExecutionRepository, IResponseTimeoutRepairPageReader, ITaskExecutionStageReader, ITaskResponseTimeoutReferenceRepository
{
    public async Task<IReadOnlyCollection<TaskExecution>> GetByStageExecutionId(Id stageExecutionId, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions.Where(x => x.StageExecutionId == stageExecutionId).ToArrayAsync(cancellationToken);

    public TaskExecutionRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
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
        => await DbContext.TaskExecutions.FindAsync([taskExecutionId], cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution '{taskExecutionId}' was not found.");

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
        => await DbContext.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution with correlation id '{correlationId}' was not found.");

    public async Task<TaskExecution> GetByStageAndKey(Id stageExecutionId, string taskKey, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions.FirstOrDefaultAsync(x => x.StageExecutionId == stageExecutionId && x.TaskKey == taskKey, cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution '{taskKey}' was not found for stage '{stageExecutionId}'.");

    public async Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .ToArrayAsync(cancellationToken);

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
                x.ResponseDeadlineUtc != null && x.ResponseDeadlineUtc <= dueBeforeUtc &&
                DbContext.OrchestrationInstances.Any(instance => instance.Id == x.OrchestrationInstanceId &&
                    (instance.Status == OrchestrationInstanceStatus.Created ||
                     instance.Status == OrchestrationInstanceStatus.Running ||
                     instance.Status == OrchestrationInstanceStatus.Idle ||
                     instance.Status == OrchestrationInstanceStatus.Waiting ||
                     instance.Status == OrchestrationInstanceStatus.Compensating)))
            .OrderBy(x => x.ResponseDeadlineUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        if (due.Length == limit) return due;

        var legacy = await DbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                x.ResponseDeadlineUtc == null && x.WaitingSinceUtc <= dueBeforeUtc &&
                DbContext.OrchestrationInstances.Any(instance => instance.Id == x.OrchestrationInstanceId &&
                    (instance.Status == OrchestrationInstanceStatus.Created ||
                     instance.Status == OrchestrationInstanceStatus.Running ||
                     instance.Status == OrchestrationInstanceStatus.Idle ||
                     instance.Status == OrchestrationInstanceStatus.Waiting ||
                     instance.Status == OrchestrationInstanceStatus.Compensating)))
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
                string.IsNullOrWhiteSpace(x.TimeoutActionId) &&
                DbContext.OrchestrationInstances.Any(instance => instance.Id == x.OrchestrationInstanceId &&
                    (instance.Status == OrchestrationInstanceStatus.Created ||
                     instance.Status == OrchestrationInstanceStatus.Running ||
                     instance.Status == OrchestrationInstanceStatus.Idle ||
                     instance.Status == OrchestrationInstanceStatus.Waiting ||
                     instance.Status == OrchestrationInstanceStatus.Compensating)))
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
                x.ResponseDeadlineUtc != null && x.ResponseDeadlineUtc <= dueBeforeUtc &&
                DbContext.OrchestrationInstances.Any(instance => instance.Id == x.OrchestrationInstanceId &&
                    (instance.Status == OrchestrationInstanceStatus.Created ||
                     instance.Status == OrchestrationInstanceStatus.Running ||
                     instance.Status == OrchestrationInstanceStatus.Idle ||
                     instance.Status == OrchestrationInstanceStatus.Waiting ||
                     instance.Status == OrchestrationInstanceStatus.Compensating)));
        var due = await projected.OrderBy(x => x.ResponseDeadlineUtc).ThenBy(x => x.Id)
            .Skip(offset).Take(limit).ToArrayAsync(cancellationToken);
        if (due.Length == limit) return due;

        var legacyOffset = offset == 0 ? 0 : Math.Max(0, offset - await projected.CountAsync(cancellationToken));
        var legacy = await DbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                x.ResponseDeadlineUtc == null && x.WaitingSinceUtc <= dueBeforeUtc &&
                DbContext.OrchestrationInstances.Any(instance => instance.Id == x.OrchestrationInstanceId &&
                    (instance.Status == OrchestrationInstanceStatus.Created ||
                     instance.Status == OrchestrationInstanceStatus.Running ||
                     instance.Status == OrchestrationInstanceStatus.Idle ||
                     instance.Status == OrchestrationInstanceStatus.Waiting ||
                     instance.Status == OrchestrationInstanceStatus.Compensating)))
            .OrderBy(x => x.WaitingSinceUtc).ThenBy(x => x.Id)
            .Skip(legacyOffset).Take(limit - due.Length).ToArrayAsync(cancellationToken);
        return due.Concat(legacy).ToArray();
    }

    public async Task<IReadOnlyCollection<TaskExecution>> GetResponseTimeoutsMissingActionPage(
        int skip, int take, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                x.ResponseDeadlineUtc != null && string.IsNullOrWhiteSpace(x.TimeoutActionId) &&
                DbContext.OrchestrationInstances.Any(instance => instance.Id == x.OrchestrationInstanceId &&
                    (instance.Status == OrchestrationInstanceStatus.Created ||
                     instance.Status == OrchestrationInstanceStatus.Running ||
                     instance.Status == OrchestrationInstanceStatus.Idle ||
                     instance.Status == OrchestrationInstanceStatus.Waiting ||
                     instance.Status == OrchestrationInstanceStatus.Compensating)))
            .OrderBy(x => x.ResponseDeadlineUtc).ThenBy(x => x.Id)
            .Skip(Math.Max(0, skip)).Take(Math.Max(1, take)).ToArrayAsync(cancellationToken);
}
