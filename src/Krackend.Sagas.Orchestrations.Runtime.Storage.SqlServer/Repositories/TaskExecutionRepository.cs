using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Repositories;

internal sealed class TaskExecutionRepository : RuntimeRepositoryBase, ITaskExecutionRepository
{
    public TaskExecutionRepository(RuntimeStorageDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
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
}
