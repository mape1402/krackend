using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class TaskExecutionAttemptRepository : RuntimeRepositoryBase, ITaskExecutionAttemptRepository, ITaskExecutionAttemptBatchReader, ITaskExecutionAttemptSummaryBatchReader
{
    private readonly bool _persistSuccessfulTransportOutcomes;
    private readonly bool _embedCurrentTransportState;

    public TaskExecutionAttemptRepository(
        RuntimeDbContext dbContext,
        IRuntimeStorageUnitOfWork unitOfWork,
        IOptions<RuntimeEntityFrameworkStorageOptions> storageOptions = null)
        : base(dbContext, unitOfWork)
    {
        _persistSuccessfulTransportOutcomes = storageOptions?.Value.PersistSuccessfulTransportOutcomes ?? true;
        _embedCurrentTransportState = storageOptions?.Value.EmbedCurrentTransportState ?? false;
    }

    public async Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
    {
        if (_embedCurrentTransportState && attempt.Id == attempt.TaskExecutionId)
        {
            var task = DbContext.TaskExecutions.Local.FirstOrDefault(x => x.Id == attempt.TaskExecutionId)
                ?? throw new RuntimeStateConflictException(
                    $"Embedded attempt '{attempt.Id}' has no tracked task execution.");
            EmbeddedTaskTransportState.StoreAttempt(task, attempt);
            await SaveChanges(cancellationToken);
            return;
        }

        DbContext.TaskExecutionAttempts.Add(attempt);
        await SaveChanges(cancellationToken);
    }

    public async Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
    {
        if (!_persistSuccessfulTransportOutcomes &&
            attempt.Status == TaskExecutionStatus.Completed)
        {
            // The completed task and aggregate snapshot already durably contain the
            // successful result. Keep attempt rows synchronous for failures/retries,
            // but do not duplicate every healthy callback write.
            DbContext.Entry(attempt).State = EntityState.Detached;
            return;
        }

        if (_embedCurrentTransportState && attempt.Id == attempt.TaskExecutionId)
        {
            var task = DbContext.TaskExecutions.Local.FirstOrDefault(x => x.Id == attempt.TaskExecutionId)
                ?? await DbContext.TaskExecutions.FirstOrDefaultAsync(
                    x => x.Id == attempt.TaskExecutionId, cancellationToken);
            if (task is not null && EmbeddedTaskTransportState.IsEmbedded(task))
            {
                EmbeddedTaskTransportState.StoreAttempt(task, attempt);
                await SaveChanges(cancellationToken);
                return;
            }
        }

        if (DbContext.Entry(attempt).State == EntityState.Detached)
        {
            DetachLocalTrackedEntity(DbContext.TaskExecutionAttempts, attempt);
            DbContext.TaskExecutionAttempts.Update(attempt);
        }
        await SaveChanges(cancellationToken);
    }

    public async Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default)
    {
        var attempt = await DbContext.TaskExecutionAttempts.FindAsync([attemptId], cancellationToken);
        if (attempt is not null) return attempt;
        if (_embedCurrentTransportState)
        {
            var task = await DbContext.TaskExecutions.FindAsync([attemptId], cancellationToken);
            if (task is not null && EmbeddedTaskTransportState.TryReadAttempt(task, out attempt)) return attempt;
        }
        throw new KeyNotFoundException($"Task execution attempt '{attemptId}' was not found.");
    }

    public async Task<TaskExecutionAttempt> GetByDispatchId(Id dispatchId, CancellationToken cancellationToken = default)
    {
        var attempt = await DbContext.TaskExecutionAttempts.FirstOrDefaultAsync(
            x => x.DispatchId == dispatchId, cancellationToken);
        if (attempt is not null) return attempt;
        if (_embedCurrentTransportState)
        {
            var task = await DbContext.TaskExecutions.FindAsync([dispatchId], cancellationToken);
            if (task is not null && EmbeddedTaskTransportState.TryReadAttempt(task, out attempt)) return attempt;
        }
        throw new KeyNotFoundException($"Task execution attempt for dispatch '{dispatchId}' was not found.");
    }

    public async Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default)
    {
        var attempts = await DbContext.TaskExecutionAttempts
            .Where(x => x.TaskExecutionId == taskExecutionId)
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync(cancellationToken);
        if (_embedCurrentTransportState)
        {
            var task = await DbContext.TaskExecutions.FindAsync([taskExecutionId], cancellationToken);
            if (task is not null && EmbeddedTaskTransportState.TryReadAttempt(task, out var embedded) &&
                attempts.All(x => x.Id != embedded.Id))
                attempts.Add(embedded);
        }
        return attempts.OrderBy(x => x.AttemptNumber).ToArray();
    }

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
