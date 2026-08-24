using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class TaskDispatchRepository : RuntimeRepositoryBase, ITaskDispatchRepository
{
    public TaskDispatchRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default)
    {
        DbContext.TaskDispatches.Add(dispatch);
        await SaveChanges(cancellationToken);
    }

    public async Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default)
    {
        DetachLocalTrackedEntity(DbContext.TaskDispatches, dispatch);
        DbContext.TaskDispatches.Update(dispatch);
        await SaveChanges(cancellationToken);
    }

    public async Task MarkSent(Id dispatchId, string status, DateTime sentOnUtc, string externalReference = null, CancellationToken cancellationToken = default)
    {
        var dispatch = await DbContext.TaskDispatches.FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found.");

        dispatch.DispatchStatus = status;
        dispatch.SentOnUtc = sentOnUtc;
        await SaveChanges(cancellationToken);
    }

    public async Task MarkFailed(Id dispatchId, string failureReason, string externalReference = null, CancellationToken cancellationToken = default)
    {
        var dispatch = await DbContext.TaskDispatches.FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found.");

        dispatch.DispatchStatus = "Failed";
        dispatch.FailedOnUtc = DateTime.UtcNow;
        dispatch.FailureReason = failureReason;
        await SaveChanges(cancellationToken);
    }

    public async Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default)
        => await DbContext.TaskDispatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found.");

    public async Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default)
        => await DbContext.TaskDispatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken);

    public async Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default)
        => await DbContext.TaskDispatches.AsNoTracking().FirstOrDefaultAsync(x => x.CommandId == commandId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task dispatch with command id '{commandId}' was not found.");

    public async Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default)
        => await DbContext.TaskDispatches.AsNoTracking().FirstOrDefaultAsync(x => x.TaskExecutionAttemptId == taskExecutionAttemptId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task dispatch for attempt '{taskExecutionAttemptId}' was not found.");

    public async Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
        => await DbContext.TaskDispatches.AsNoTracking()
            .Where(x => x.ScheduledOnUtc <= dueBeforeUtc)
            .ToArrayAsync(cancellationToken);
}
