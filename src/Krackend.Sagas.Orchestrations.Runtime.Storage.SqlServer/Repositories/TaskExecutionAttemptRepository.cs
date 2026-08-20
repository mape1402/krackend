using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Repositories;

internal sealed class TaskExecutionAttemptRepository : RuntimeRepositoryBase, ITaskExecutionAttemptRepository
{
    public TaskExecutionAttemptRepository(RuntimeStorageDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
    {
        DbContext.TaskExecutionAttempts.Add(attempt);
        await SaveChanges(cancellationToken);
    }

    public async Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
    {
        DbContext.TaskExecutionAttempts.Update(attempt);
        await SaveChanges(cancellationToken);
    }

    public async Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutionAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attemptId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution attempt '{attemptId}' was not found.");

    public async Task<TaskExecutionAttempt> GetByDispatchId(Id dispatchId, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutionAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.DispatchId == dispatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task execution attempt for dispatch '{dispatchId}' was not found.");

    public async Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutionAttempts.AsNoTracking()
            .Where(x => x.TaskExecutionId == taskExecutionId)
            .OrderBy(x => x.AttemptNumber)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutionAttempts.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse && x.WaitingSinceUtc <= dueBeforeUtc)
            .ToArrayAsync(cancellationToken);
}
