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
        var entry = DbContext.Entry(instance);
        entry.Property(x => x.ActiveLeaseId).IsModified = false;
        entry.Property(x => x.ActiveLeaseExpiresOnUtc).IsModified = false;
        await SaveChanges(cancellationToken);
    }

    public async Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");

    public async Task<OrchestrationInstance> TryGetByStartIdempotencyKey(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(startIdempotencyKey))
        {
            return null;
        }

        var normalizedKey = startIdempotencyKey.Trim();
        return await DbContext.OrchestrationInstances.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StartIdempotencyKey == normalizedKey, cancellationToken);
    }

    public async Task<OrchestrationInstanceLease> TryAcquireLease(Id instanceId, string leaseId, DateTime nowUtc, DateTime expiresOnUtc, CancellationToken cancellationToken = default)
    {
        if (DbContext.Database.IsRelational())
        {
            var affected = await DbContext.OrchestrationInstances
                .Where(x =>
                    x.Id == instanceId &&
                    (x.ActiveLeaseId == null ||
                     x.ActiveLeaseId == string.Empty ||
                     x.ActiveLeaseId == leaseId ||
                     x.ActiveLeaseExpiresOnUtc == null ||
                     x.ActiveLeaseExpiresOnUtc <= nowUtc))
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(x => x.ActiveLeaseId, leaseId)
                    .SetProperty(x => x.ActiveLeaseExpiresOnUtc, expiresOnUtc),
                    cancellationToken);

            if (affected == 1)
            {
                return new OrchestrationInstanceLease(instanceId, leaseId, nowUtc, expiresOnUtc);
            }

            var exists = await DbContext.OrchestrationInstances
                .AsNoTracking()
                .AnyAsync(x => x.Id == instanceId, cancellationToken);
            if (!exists)
            {
                throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");
            }

            return null;
        }

        var instance = await DbContext.OrchestrationInstances.FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");

        if (!string.IsNullOrWhiteSpace(instance.ActiveLeaseId) &&
            !string.Equals(instance.ActiveLeaseId, leaseId, StringComparison.Ordinal) &&
            instance.ActiveLeaseExpiresOnUtc > nowUtc)
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
        if (DbContext.Database.IsRelational())
        {
            await DbContext.OrchestrationInstances
                .Where(x => x.Id == instanceId && x.ActiveLeaseId == leaseId)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(x => x.ActiveLeaseId, (string)null)
                    .SetProperty(x => x.ActiveLeaseExpiresOnUtc, (DateTime?)null),
                    cancellationToken);
            return;
        }

        var instance = await DbContext.OrchestrationInstances.FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        if (instance is not null && instance.ActiveLeaseId == leaseId)
        {
            instance.ActiveLeaseId = null;
            instance.ActiveLeaseExpiresOnUtc = null;
            await SaveChanges(cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(int take = 50, CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking()
            .OrderByDescending(x => x.LastUpdatedOnUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<OrchestrationInstance>> GetRecoverable(int take = 100, CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x =>
                x.Status != OrchestrationInstanceStatus.Stopped &&
                x.Status != OrchestrationInstanceStatus.Compensated &&
                x.Status != OrchestrationInstanceStatus.Completed &&
                x.Status != OrchestrationInstanceStatus.CompletedWithErrors &&
                x.Status != OrchestrationInstanceStatus.DeadLettered &&
                x.Status != OrchestrationInstanceStatus.Aborted &&
                x.Status != OrchestrationInstanceStatus.Failed)
            .OrderBy(x => x.LastUpdatedOnUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public async Task<RuntimeInstanceSummary> GetSummary(DateTime recentSinceUtc, CancellationToken cancellationToken = default)
    {
        var query = DbContext.OrchestrationInstances.AsNoTracking();
        return new RuntimeInstanceSummary(
            await query.CountAsync(x => x.Status == OrchestrationInstanceStatus.Created || x.Status == OrchestrationInstanceStatus.Running, cancellationToken),
            await query.CountAsync(x => x.Status == OrchestrationInstanceStatus.Waiting, cancellationToken),
            await query.CountAsync(x => x.CompletedOnUtc >= recentSinceUtc, cancellationToken),
            await query.CountAsync(x => x.FailedOnUtc >= recentSinceUtc || x.Status == OrchestrationInstanceStatus.DeadLettered, cancellationToken),
            recentSinceUtc);
    }
}
