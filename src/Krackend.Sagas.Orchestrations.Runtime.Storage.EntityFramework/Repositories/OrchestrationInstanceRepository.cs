using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class OrchestrationInstanceRepository : RuntimeRepositoryBase, IOrchestrationInstanceRepository, IOrchestrationIdleInstanceRepository, IOrchestrationDispatchCancellationIntentRepository, IRuntimeInstanceCatalogRepository, IRuntimeInstanceStatusCountsRepository
{
    public OrchestrationInstanceRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default)
    {
        instance.CaptureLastExecutionCursor();
        instance.StatisticsRevision = 1;
        DbContext.OrchestrationInstances.Add(instance);
        AddStatisticsObservation(instance, false);
        await SaveChanges(cancellationToken);
    }

    public async Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default)
    {
        instance.CaptureLastExecutionCursor();
        var existing = DbContext.OrchestrationInstances.Local.FirstOrDefault(x => x.Id == instance.Id);
        var originalRevision = existing is null
            ? instance.StatisticsRevision
            : DbContext.Entry(existing).Property(x => x.StatisticsRevision).OriginalValue;
        instance.StatisticsRevision = Math.Max(instance.StatisticsRevision, existing?.StatisticsRevision ?? 0) + 1;
        DetachLocalTrackedEntity(DbContext.OrchestrationInstances, instance);
        DbContext.OrchestrationInstances.Update(instance);
        var entry = DbContext.Entry(instance);
        entry.Property(x => x.StatisticsRevision).OriginalValue = originalRevision;
        entry.Property(x => x.ActiveLeaseId).IsModified = false;
        entry.Property(x => x.ActiveLeaseExpiresOnUtc).IsModified = false;
        AddStatisticsObservation(instance, originalRevision == 0 && instance.StatisticsRevision == 1);
        await SaveChanges(cancellationToken);
    }

    private void AddStatisticsObservation(OrchestrationInstance instance, bool isBackfill)
    {
        DbContext.SagaStatisticsObservations.Add(new SagaStatisticsObservation
        {
            Id = $"{instance.Id}:{instance.StatisticsRevision:D20}",
            InstanceId = instance.Id,
            ArtifactId = instance.RuntimeOrchestrationArtifactId,
            OrchestrationKey = instance.OrchestrationDefinitionKey,
            Revision = instance.StatisticsRevision,
            Status = instance.Status,
            StageKey = instance.CurrentStageKey,
            StartedOnUtc = instance.StartedOnUtc,
            OccurredOnUtc = instance.LastUpdatedOnUtc,
            IsBackfill = isBackfill
        });
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

    public async Task<RuntimeInstanceCatalogPage> GetPage(RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.PageNumber);
        var size = Math.Clamp(query.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * size, int.MaxValue);
        var source = ApplyCatalogFilters(query);
        var count = await source.LongCountAsync(cancellationToken);
        var ordered = (query.Sort, query.Descending) switch
        {
            (RuntimeInstanceCatalogSort.Started, false) => source.OrderBy(x => x.StartedOnUtc),
            (RuntimeInstanceCatalogSort.Started, true) => source.OrderByDescending(x => x.StartedOnUtc),
            (RuntimeInstanceCatalogSort.Orchestration, false) => source.OrderBy(x => x.OrchestrationDefinitionKey),
            (RuntimeInstanceCatalogSort.Orchestration, true) => source.OrderByDescending(x => x.OrchestrationDefinitionKey),
            (RuntimeInstanceCatalogSort.CorrelationId, false) => source.OrderBy(x => x.CorrelationId),
            (RuntimeInstanceCatalogSort.CorrelationId, true) => source.OrderByDescending(x => x.CorrelationId),
            (RuntimeInstanceCatalogSort.SagaId, false) => source.OrderBy(x => x.SagaId),
            (RuntimeInstanceCatalogSort.SagaId, true) => source.OrderByDescending(x => x.SagaId),
            (RuntimeInstanceCatalogSort.Status, false) => source.OrderBy(x => x.Status),
            (RuntimeInstanceCatalogSort.Status, true) => source.OrderByDescending(x => x.Status),
            (_, false) => source.OrderBy(x => x.LastUpdatedOnUtc),
            _ => source.OrderByDescending(x => x.LastUpdatedOnUtc)
        };
        var items = await ordered.ThenBy(x => x.Id).Skip(skip).Take(size).ToArrayAsync(cancellationToken);
        return new RuntimeInstanceCatalogPage(items, count, page, size);
    }

    public async Task<IReadOnlyDictionary<OrchestrationInstanceStatus, long>> GetStatusCounts(
        RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await ApplyCatalogFilters(query).GroupBy(x => x.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
    }

    private IQueryable<OrchestrationInstance> ApplyCatalogFilters(RuntimeInstanceCatalogQuery query)
    {
        var source = DbContext.OrchestrationInstances.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var term = query.SearchText.Trim();
            if (Ulid.TryParse(term, out var parsedId))
            {
                var instanceId = new Id(parsedId);
                source = source.Where(x => x.Id == instanceId || x.OrchestrationDefinitionKey.Contains(term)
                    || x.CorrelationId.Contains(term) || x.SagaId.Contains(term) || x.ExecutionKey.Contains(term));
            }
            else
                source = source.Where(x => x.OrchestrationDefinitionKey.Contains(term) || x.CorrelationId.Contains(term)
                    || x.SagaId.Contains(term) || x.ExecutionKey.Contains(term));
        }
        if (query.Status is { } status) source = source.Where(x => x.Status == status);
        if (query.StartedFromUtc is { } startedFrom) source = source.Where(x => x.StartedOnUtc >= startedFrom);
        if (query.StartedBeforeUtc is { } startedBefore) source = source.Where(x => x.StartedOnUtc < startedBefore);
        if (query.ActiveOnly)
            source = source.Where(x => x.Status == OrchestrationInstanceStatus.Created
                || x.Status == OrchestrationInstanceStatus.Running
                || x.Status == OrchestrationInstanceStatus.Idle
                || x.Status == OrchestrationInstanceStatus.Compensating);
        return source;
    }

    public async Task<IReadOnlyCollection<OrchestrationInstance>> GetDispatchCancellationPending(int take, int skip, CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.DispatchCancellationPending)
            .OrderBy(x => x.LastUpdatedOnUtc)
            .ThenBy(x => x.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<OrchestrationInstance>> GetIdleExpiringOnOrBefore(
        DateTime utcNow,
        int take = 100,
        CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.Status == OrchestrationInstanceStatus.Idle &&
                        x.IdleExpiresOnUtc != null &&
                        x.IdleExpiresOnUtc <= utcNow)
            .OrderBy(x => x.IdleExpiresOnUtc)
            .ThenBy(x => x.Id)
            .Take(Math.Max(1, take))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<OrchestrationInstance>> GetRecoverable(int take = 100, CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x =>
                x.Status != OrchestrationInstanceStatus.Stopped &&
                x.Status != OrchestrationInstanceStatus.Compensated &&
                x.Status != OrchestrationInstanceStatus.Completed &&
                x.Status != OrchestrationInstanceStatus.CompletedWithErrors &&
                x.Status != OrchestrationInstanceStatus.Failed &&
                x.Status != OrchestrationInstanceStatus.DeadLettered &&
                x.Status != OrchestrationInstanceStatus.Aborted)
            .OrderBy(x => x.LastUpdatedOnUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public async Task<RuntimeInstanceSummary> GetSummary(DateTime recentSinceUtc, CancellationToken cancellationToken = default)
    {
        var query = DbContext.OrchestrationInstances.AsNoTracking();
        return new RuntimeInstanceSummary(
            await query.CountAsync(x => x.Status == OrchestrationInstanceStatus.Created || x.Status == OrchestrationInstanceStatus.Running, cancellationToken),
            await query.CountAsync(x => x.Status == OrchestrationInstanceStatus.Idle, cancellationToken),
            await query.CountAsync(x => x.CompletedOnUtc >= recentSinceUtc, cancellationToken),
            await query.CountAsync(x => x.FailedOnUtc >= recentSinceUtc || x.Status == OrchestrationInstanceStatus.DeadLettered, cancellationToken),
            recentSinceUtc);
    }
}
