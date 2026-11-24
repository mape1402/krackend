using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class CompensationExecutionRepository : RuntimeRepositoryBase, ICompensationExecutionRepository
{
    public CompensationExecutionRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task Create(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
    {
        DbContext.CompensationExecutions.Add(compensationExecution);
        await SaveChanges(cancellationToken);
    }

    public async Task Update(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
    {
        // Compensation is created and enriched with its durable dispatch intent in
        // the same orchestration activation. While the activation batch is deferred,
        // that entity is still Added and must remain an INSERT. Calling Update here
        // changes it to Modified, producing an UPDATE for a row that does not exist
        // yet and an optimistic-concurrency failure at the batch commit.
        if (DbContext.Entry(compensationExecution).State == EntityState.Detached)
        {
            DetachLocalTrackedEntity(DbContext.CompensationExecutions, compensationExecution);
            DbContext.CompensationExecutions.Update(compensationExecution);
        }
        await SaveChanges(cancellationToken);
    }

    public async Task<CompensationExecution> TryGetById(Id compensationExecutionId, CancellationToken cancellationToken = default)
        => await DbContext.CompensationExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == compensationExecutionId, cancellationToken);

    public async Task<IReadOnlyCollection<CompensationExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await DbContext.CompensationExecutions.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .OrderByDescending(x => x.StartedOnUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<CompensationExecution>> GetPending(CancellationToken cancellationToken = default)
        => await DbContext.CompensationExecutions.AsNoTracking()
            .Where(x => x.Status == "Pending")
            .ToArrayAsync(cancellationToken);
}
