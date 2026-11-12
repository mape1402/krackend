using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class TaskExecutionRepository : RuntimeRepositoryBase, ITaskExecutionRepository
{
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
        DetachLocalTrackedEntity(DbContext.TaskExecutions, taskExecution);
        DbContext.TaskExecutions.Update(taskExecution);
        await SaveChanges(cancellationToken);
    }

    public async Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskExecutionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution '{taskExecutionId}' was not found.");

    public async Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution with correlation id '{correlationId}' was not found.");

    public async Task<TaskExecution> GetByStageAndKey(Id stageExecutionId, string taskKey, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.StageExecutionId == stageExecutionId && x.TaskKey == taskKey, cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution '{taskKey}' was not found for stage '{stageExecutionId}'.");

    public async Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutions.AsNoTracking()
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
}
