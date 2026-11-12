using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class TaskExecutionAttemptRepository : RuntimeRepositoryBase, ITaskExecutionAttemptRepository, ITaskExecutionAttemptBatchReader, ITaskExecutionAttemptSummaryBatchReader
{
    public TaskExecutionAttemptRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
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
        DetachLocalTrackedEntity(DbContext.TaskExecutionAttempts, attempt);
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

    public async Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionIds(
        IReadOnlyCollection<Id> taskExecutionIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskExecutionIds);
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<TaskExecutionAttempt>();
        foreach (var batch in taskExecutionIds.Distinct().Chunk(200))
            results.AddRange(await DbContext.TaskExecutionAttempts.AsNoTracking()
                .Where(x => batch.Contains(x.TaskExecutionId)).ToArrayAsync(cancellationToken));
        return results;
    }

    public async Task<IReadOnlyCollection<TaskExecutionAttempt>> GetSummariesByTaskExecutionIds(
        IReadOnlyCollection<Id> taskExecutionIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskExecutionIds);
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<TaskExecutionAttempt>();
        foreach (var batch in taskExecutionIds.Distinct().Chunk(200))
        {
            var rows = await DbContext.TaskExecutionAttempts.AsNoTracking()
                .Where(x => batch.Contains(x.TaskExecutionId))
                .Select(x => new
                {
                    x.Id, x.TaskExecutionId, x.AttemptNumber, x.Status, x.StartedOnUtc, x.WaitingSinceUtc,
                    x.CompletedOnUtc, x.FailedOnUtc, x.TimedOutOnUtc, x.ErrorCode, x.ErrorMessage
                }).ToArrayAsync(cancellationToken);
            results.AddRange(rows.Select(x => new TaskExecutionAttempt
            {
                Id = x.Id, TaskExecutionId = x.TaskExecutionId, AttemptNumber = x.AttemptNumber,
                Status = x.Status, StartedOnUtc = x.StartedOnUtc, WaitingSinceUtc = x.WaitingSinceUtc,
                CompletedOnUtc = x.CompletedOnUtc, FailedOnUtc = x.FailedOnUtc, TimedOutOnUtc = x.TimedOutOnUtc,
                ErrorCode = x.ErrorCode, ErrorMessage = x.ErrorMessage
            }));
        }
        return results;
    }

    public async Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
        => await DbContext.TaskExecutionAttempts.AsNoTracking()
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse && x.WaitingSinceUtc <= dueBeforeUtc)
            .ToArrayAsync(cancellationToken);
}
