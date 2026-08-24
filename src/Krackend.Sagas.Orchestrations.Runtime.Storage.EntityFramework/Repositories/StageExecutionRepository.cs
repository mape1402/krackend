using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class StageExecutionRepository : RuntimeRepositoryBase, IStageExecutionRepository
{
    public StageExecutionRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default)
    {
        DbContext.StageExecutions.Add(stageExecution);
        await SaveChanges(cancellationToken);
    }

    public async Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default)
    {
        DetachLocalTrackedEntity(DbContext.StageExecutions, stageExecution);
        DbContext.StageExecutions.Update(stageExecution);
        await SaveChanges(cancellationToken);
    }

    public async Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default)
        => await DbContext.StageExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == stageExecutionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage execution '{stageExecutionId}' was not found.");

    public async Task<StageExecution> GetByInstanceAndKey(Id instanceId, string stageKey, CancellationToken cancellationToken = default)
        => await DbContext.StageExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.OrchestrationInstanceId == instanceId && x.StageKey == stageKey, cancellationToken)
            ?? throw new KeyNotFoundException($"Stage execution '{stageKey}' was not found for instance '{instanceId}'.");

    public async Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await DbContext.StageExecutions.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .OrderBy(x => x.Order)
            .ToArrayAsync(cancellationToken);
}
