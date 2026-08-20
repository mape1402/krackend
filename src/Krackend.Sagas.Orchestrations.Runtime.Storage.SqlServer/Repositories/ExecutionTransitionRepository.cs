using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Repositories;

internal sealed class ExecutionTransitionRepository : RuntimeRepositoryBase, IExecutionTransitionRepository
{
    public ExecutionTransitionRepository(RuntimeStorageDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default)
    {
        DbContext.ExecutionTransitions.Add(transition);
        await SaveChanges(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await DbContext.ExecutionTransitions.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .OrderBy(x => x.OccurredOnUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(string environmentKey, int take = 250, CancellationToken cancellationToken = default)
        => await DbContext.ExecutionTransitions.AsNoTracking()
            .OrderByDescending(x => x.OccurredOnUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(string environmentKey, DateTime sinceUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeTrafficPoint>>([]);
}
