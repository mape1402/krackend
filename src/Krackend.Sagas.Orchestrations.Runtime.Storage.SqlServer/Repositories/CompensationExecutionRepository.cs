using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Repositories;

internal sealed class CompensationExecutionRepository : RuntimeRepositoryBase, ICompensationExecutionRepository
{
    public CompensationExecutionRepository(RuntimeStorageDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
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
        DbContext.CompensationExecutions.Update(compensationExecution);
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
