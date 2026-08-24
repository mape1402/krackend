using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class OrchestrationInstanceRepository : RuntimeRepositoryBase, IOrchestrationInstanceRepository
{
    public OrchestrationInstanceRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default)
    {
        DbContext.OrchestrationInstances.Add(instance);
        await SaveChanges(cancellationToken);
    }

    public async Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default)
    {
        DetachLocalTrackedEntity(DbContext.OrchestrationInstances, instance);
        DbContext.OrchestrationInstances.Update(instance);
        await SaveChanges(cancellationToken);
    }

    public async Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");

    public async Task<OrchestrationInstanceLease> TryAcquireLease(Id instanceId, string leaseId, DateTime nowUtc, DateTime expiresOnUtc, CancellationToken cancellationToken = default)
    {
        var instance = await DbContext.OrchestrationInstances.FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");

        if (!string.IsNullOrWhiteSpace(instance.ActiveLeaseId) && instance.ActiveLeaseExpiresOnUtc > nowUtc)
        {
            return null;
        }

        instance.ActiveLeaseId = leaseId;
        instance.ActiveLeaseExpiresOnUtc = expiresOnUtc;
        await SaveChanges(cancellationToken);
        return new OrchestrationInstanceLease(instanceId, leaseId, nowUtc, expiresOnUtc);
    }

    public async Task ReleaseLease(Id instanceId, string leaseId, CancellationToken cancellationToken = default)
    {
        var instance = await DbContext.OrchestrationInstances.FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        if (instance is not null && instance.ActiveLeaseId == leaseId)
        {
            instance.ActiveLeaseId = null;
            instance.ActiveLeaseExpiresOnUtc = null;
            await SaveChanges(cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(string environmentKey, int take = 50, CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.EnvironmentKey == environmentKey)
            .OrderByDescending(x => x.LastUpdatedOnUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public async Task<RuntimeInstanceSummary> GetSummary(string environmentKey, DateTime recentSinceUtc, CancellationToken cancellationToken = default)
    {
        var query = DbContext.OrchestrationInstances.AsNoTracking().Where(x => x.EnvironmentKey == environmentKey);
        return new RuntimeInstanceSummary(
            await query.CountAsync(x => x.Status == OrchestrationInstanceStatus.Created || x.Status == OrchestrationInstanceStatus.Running, cancellationToken),
            await query.CountAsync(x => x.Status == OrchestrationInstanceStatus.Waiting, cancellationToken),
            await query.CountAsync(x => x.CompletedOnUtc >= recentSinceUtc, cancellationToken),
            await query.CountAsync(x => x.FailedOnUtc >= recentSinceUtc, cancellationToken),
            recentSinceUtc);
    }
}
