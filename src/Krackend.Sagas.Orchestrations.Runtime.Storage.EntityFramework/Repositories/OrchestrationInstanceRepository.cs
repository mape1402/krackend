using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Data;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.HotState;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class OrchestrationInstanceRepository : RuntimeRepositoryBase, IOrchestrationInstanceRepository, IAtomicStartIdempotencyRepository, IOrchestrationIdleInstanceRepository, IOrchestrationLifetimeCandidateRepository, IOrchestrationLifetimeScanRepository, IOrchestrationExpirationRepository, IOrchestrationArtifactInstanceReader, IOrchestrationArtifactActivityBatchReader, IOrchestrationDispatchCancellationIntentRepository, IRuntimeInstanceCatalogRepository, IRuntimeInstanceStatusCountsRepository, IRuntimeInstanceRecentSummaryReader, IOrchestrationProgressRecoveryCandidateRepository
{
    private static readonly ConcurrentDictionary<string, bool> ProvidersWithoutStatusGrouping = new();
    private readonly bool _persistStatisticsObservations;
    private readonly IRuntimeHotStateStore _hotState;

    public OrchestrationInstanceRepository(
        RuntimeDbContext dbContext,
        IRuntimeStorageUnitOfWork unitOfWork,
        IOptions<RuntimeEntityFrameworkStorageOptions> storageOptions = null,
        IRuntimeHotStateStore hotState = null)
        : base(dbContext, unitOfWork)
    {
        _persistStatisticsObservations = storageOptions?.Value.PersistStatisticsObservations ?? true;
        _hotState = hotState;
    }

    public async Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default)
    {
        instance.CaptureLastExecutionCursor();
        instance.StatisticsRevision = 1;
        DbContext.OrchestrationInstances.Add(instance);
        if (_persistStatisticsObservations)
            AddStatisticsObservation(instance, false);
        await SaveChanges(cancellationToken);
        instance.MarkStatisticsStatusObserved();
    }

    public async Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default)
    {
        instance.CaptureLastExecutionCursor();
        var statusChanged = instance.HasUnobservedStatisticsStatusChange();
        var existing = DbContext.OrchestrationInstances.Local.FirstOrDefault(x => x.Id == instance.Id);
        var originalRevision = ReferenceEquals(existing, instance)
            ? DbContext.Entry(existing).Property(x => x.StatisticsRevision).OriginalValue
            : instance.StatisticsRevision;
        instance.StatisticsRevision = Math.Max(instance.StatisticsRevision, existing?.StatisticsRevision ?? 0) + 1;
        if (DbContext.Entry(instance).State == EntityState.Detached)
        {
            DetachLocalTrackedEntity(DbContext.OrchestrationInstances, instance);
            DbContext.OrchestrationInstances.Update(instance);
            // Cached decision state is intentionally detached from the request scope.
            // Update() otherwise sends every column back to SQL, including large stable
            // identity strings, on every callback. Keep the aggregate's immutable
            // identity out of the hot UPDATE while all lifecycle fields remain tracked.
            var detachedEntry = DbContext.Entry(instance);
            detachedEntry.Property(x => x.OrchestrationDefinitionKey).IsModified = false;
            detachedEntry.Property(x => x.RuntimeOrchestrationArtifactId).IsModified = false;
            detachedEntry.Property(x => x.TriggerIntakeId).IsModified = false;
            detachedEntry.Property(x => x.StartIdempotencyKey).IsModified = false;
            detachedEntry.Property(x => x.CorrelationId).IsModified = false;
            detachedEntry.Property(x => x.SagaId).IsModified = false;
            detachedEntry.Property(x => x.ExecutionKey).IsModified = false;
            detachedEntry.Property(x => x.StartedOnUtc).IsModified = false;
        }
        var entry = DbContext.Entry(instance);
        entry.Property(x => x.StatisticsRevision).OriginalValue = originalRevision;
        entry.Property(x => x.ActiveLeaseId).IsModified = false;
        entry.Property(x => x.ActiveLeaseExpiresOnUtc).IsModified = false;
        if (statusChanged && _persistStatisticsObservations)
            AddStatisticsObservation(instance, originalRevision == 0 && instance.StatisticsRevision == 1);
        await SaveChanges(cancellationToken);
        if (statusChanged)
            instance.MarkStatisticsStatusObserved();
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
    {
        // A single activation can persist a timeout and immediately evaluate its
        // retry/compensation decision before the outer activation batch is flushed.
        // Its tracked aggregate is the authoritative pending state. Reading another
        // AsNoTracking snapshot here would return the pre-activation revision, detach
        // the pending aggregate on Update, and fail the eventual optimistic write.
        var tracked = DbContext.OrchestrationInstances.Local.FirstOrDefault(x => x.Id == instanceId);
        var hasTrackedExecutionGraph = tracked is not null &&
            (DbContext.StageExecutions.Local.Any(stage => stage.OrchestrationInstanceId == instanceId) ||
             DbContext.TaskExecutions.Local.Any(task => task.OrchestrationInstanceId == instanceId));
        if (tracked is not null &&
            (DbContext.Entry(tracked).State != EntityState.Unchanged || hasTrackedExecutionGraph))
        {
            return tracked;
        }
        // ExecuteUpdate and another DbContext can advance the durable row without
        // updating this scope's identity map. An isolated unchanged local copy is
        // therefore only a cache, not pending work. A locally tracked execution
        // graph still identifies the same in-flight activation and must retain
        // object identity. Hot-state activations remain safe because their latest
        // accepted version is read from Redis immediately below.
        if (tracked is not null)
            DbContext.Entry(tracked).State = EntityState.Detached;

        if (_hotState?.IsEnabled == true)
        {
            var hot = RuntimeHotStateEntityCodec.Deserialize<OrchestrationInstance>(
                await _hotState.TryReadAsync(typeof(OrchestrationInstance).FullName!, instanceId.ToString(), cancellationToken));
            if (hot is not null) return hot;
        }

        var durable = await DbContext.OrchestrationInstances.AsNoTracking()
                   .FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken)
               ?? throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");
        if (_hotState?.IsEnabled == true)
            await _hotState.CacheAsync(RuntimeHotStateEntityCodec.CreateCacheMutation(durable), cancellationToken);
        return durable;
    }

    public async Task<OrchestrationInstance> TryGetByStartIdempotencyKey(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(startIdempotencyKey))
        {
            return null;
        }

        var normalizedKey = startIdempotencyKey.Trim();
        if (_hotState?.IsEnabled == true)
        {
            var hot = RuntimeHotStateEntityCodec.Deserialize<OrchestrationInstance>(
                await _hotState.TryReadByIndexAsync(typeof(OrchestrationInstance).FullName!, "start", normalizedKey, cancellationToken));
            if (hot is not null) return hot;
        }
        var durable = await DbContext.OrchestrationInstances.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StartIdempotencyKey == normalizedKey, cancellationToken);
        if (durable is not null && _hotState?.IsEnabled == true)
            await _hotState.CacheAsync(RuntimeHotStateEntityCodec.CreateCacheMutation(durable), cancellationToken);
        return durable;
    }

    public async Task<OrchestrationInstanceLease> TryAcquireLease(Id instanceId, string leaseId, DateTime nowUtc, DateTime expiresOnUtc, CancellationToken cancellationToken = default)
    {
        if (_hotState?.IsEnabled == true)
        {
            var duration = expiresOnUtc - nowUtc;
            if (duration <= TimeSpan.Zero) return null;
            return await _hotState.TryAcquireLeaseAsync(instanceId, leaseId, duration, cancellationToken)
                ? new OrchestrationInstanceLease(instanceId, leaseId, nowUtc, expiresOnUtc)
                : null;
        }
        var transactionLockResult = await TryAcquireSqlServerTransactionLock(
            instanceId, leaseId, cancellationToken);
        if (transactionLockResult.HasValue)
        {
            return transactionLockResult.Value
                ? new OrchestrationInstanceLease(instanceId, leaseId, nowUtc, expiresOnUtc)
                : null;
        }

        var affected = await DbContext.OrchestrationInstances
            .Where(x => x.Id == instanceId &&
                (x.ActiveLeaseId == null || x.ActiveLeaseId == string.Empty || x.ActiveLeaseId == leaseId ||
                 x.ActiveLeaseExpiresOnUtc == null || x.ActiveLeaseExpiresOnUtc <= nowUtc))
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(x => x.ActiveLeaseId, leaseId)
                .SetProperty(x => x.ActiveLeaseExpiresOnUtc, expiresOnUtc), cancellationToken);
        if (affected == 1)
        {
            // The conditional acquisition already fenced and locked this instance in
            // the current activation transaction. Let SaveChanges reuse that guard
            // instead of issuing the same conditional UPDATE again.
            DbContext.MarkMutationLeaseGuarded(instanceId, leaseId);
            return new OrchestrationInstanceLease(instanceId, leaseId, nowUtc, expiresOnUtc);
        }

        // Some providers report modified rather than matched rows. A repeated
        // renewal with the same expiry may therefore report zero changes.
        var current = await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.Id == instanceId)
            .Select(x => new { x.ActiveLeaseId, x.ActiveLeaseExpiresOnUtc })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");
        if (string.Equals(current.ActiveLeaseId, leaseId, StringComparison.Ordinal) && current.ActiveLeaseExpiresOnUtc > nowUtc)
        {
            DbContext.MarkMutationLeaseGuarded(instanceId, leaseId);
            return new OrchestrationInstanceLease(instanceId, leaseId, nowUtc, current.ActiveLeaseExpiresOnUtc.Value);
        }
        return null;
    }

    public async Task ReleaseLease(Id instanceId, string leaseId, CancellationToken cancellationToken = default)
    {
        if (_hotState?.IsEnabled == true)
        {
            await _hotState.ReleaseLeaseAsync(instanceId, leaseId, cancellationToken);
            return;
        }
        if (DbContext.IsGuardedByTransactionLock(instanceId, leaseId))
        {
            // SQL Server releases transaction-owned application locks automatically on
            // commit or rollback. Keeping it until then fences the complete mutation.
            return;
        }

        // Coordinated engine mutations already hold the instance row lock and finish
        // with one SaveChanges. If that instance is tracked, fold lease release into
        // the functional instance UPDATE instead of issuing a second UPDATE against
        // the same row immediately before the flush. Standalone callers still require
        // the immediate conditional update below.
        if (DbContext.Database.CurrentTransaction is not null)
        {
            var tracked = DbContext.OrchestrationInstances.Local.FirstOrDefault(x =>
                x.Id == instanceId && string.Equals(x.ActiveLeaseId, leaseId, StringComparison.Ordinal));
            if (tracked is not null)
            {
                tracked.ActiveLeaseId = null;
                tracked.ActiveLeaseExpiresOnUtc = null;
                var entry = DbContext.Entry(tracked);
                entry.Property(x => x.ActiveLeaseId).IsModified = true;
                entry.Property(x => x.ActiveLeaseExpiresOnUtc).IsModified = true;
                return;
            }
        }

        await DbContext.OrchestrationInstances
            .Where(x => x.Id == instanceId && x.ActiveLeaseId == leaseId)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(x => x.ActiveLeaseId, (string)null)
                .SetProperty(x => x.ActiveLeaseExpiresOnUtc, (DateTime?)null), cancellationToken);
    }

    private async Task<bool?> TryAcquireSqlServerTransactionLock(
        Id instanceId,
        string leaseId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                DbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal) ||
            DbContext.Database.CurrentTransaction is null)
        {
            return null;
        }

        var connection = DbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = DbContext.Database.CurrentTransaction.GetDbTransaction();
        command.CommandType = CommandType.Text;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 0,
                @DbPrincipal = N'public';
            SELECT @result;
            """;
        var resource = command.CreateParameter();
        resource.ParameterName = "@resource";
        resource.DbType = DbType.String;
        resource.Size = 255;
        resource.Value = $"Krackend:OrchestrationInstance:{instanceId}";
        command.Parameters.Add(resource);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var result = Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);
        if (result >= 0)
        {
            DbContext.MarkMutationLeaseGuarded(
                instanceId, leaseId, guardedByTransactionLock: true);
            return true;
        }

        if (result is -1 or -2 or -3)
            return false;

        throw new InvalidOperationException(
            $"SQL Server could not acquire the orchestration transaction lock for '{instanceId}' (result {result}).");
    }

    public async Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(int take = 50, CancellationToken cancellationToken = default)
    {
        var size = Math.Max(1, take);
        if (_hotState?.IsEnabled != true)
            return await DbContext.OrchestrationInstances.AsNoTracking()
                .OrderByDescending(x => x.LastUpdatedOnUtc).Take(size).ToArrayAsync(cancellationToken);
        var overlays = await GetPendingHotInstances(cancellationToken);
        var durable = await DbContext.OrchestrationInstances.AsNoTracking()
            .OrderByDescending(x => x.LastUpdatedOnUtc).ThenByDescending(x => x.Id)
            .Take(size + overlays.Length).ToArrayAsync(cancellationToken);
        return durable.Concat(overlays).GroupBy(x => x.Id).Select(group => group.Last())
            .OrderByDescending(x => x.LastUpdatedOnUtc).ThenByDescending(x => x.Id).Take(size).ToArray();
    }

    public async Task<IReadOnlyCollection<OrchestrationInstance>> GetRecentSummaries(int take, CancellationToken cancellationToken = default)
        => await GetRecent(take, cancellationToken);

    public async Task<RuntimeInstanceCatalogPage> GetPage(RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.PageNumber);
        var size = Math.Clamp(query.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * size, int.MaxValue);
        var source = ApplyCatalogFilters(query);
        if (_hotState?.IsEnabled == true)
        {
            var overlays = await GetPendingHotInstances(cancellationToken);
            var window = (int)Math.Min((long)skip + size + overlays.Length + 1, 100_000);
            var orderedSource = ApplyCatalogOrder(source, query);
            var durable = await ReadSummaries(orderedSource.Take(window), cancellationToken);
            var merged = durable.ToDictionary(item => item.Id);
            foreach (var overlay in overlays) merged[overlay.Id] = overlay;
            var logical = ApplyCatalogOrder(merged.Values.Where(item => MatchesCatalog(item, query)), query).ToArray();
            var hotRows = logical.Skip(skip).Take(size + 1).ToArray();
            var hotCount = query.IncludeTotalCount
                ? await GetLogicalCount(source, overlays, query, cancellationToken)
                : -1;
            var hotHasMore = query.IncludeTotalCount ? (long)page * size < hotCount : hotRows.Length > size;
            return new RuntimeInstanceCatalogPage(hotRows.Take(size).ToArray(), hotCount, page, size, hotHasMore);
        }
        var count = query.IncludeTotalCount
            ? await source.LongCountAsync(cancellationToken)
            : -1;
        var deterministic = ApplyCatalogOrder(source, query);
        var rows = await ReadSummaries(
            deterministic.Skip(skip).Take(query.IncludeTotalCount ? size : size + 1),
            cancellationToken);
        var hasMore = query.IncludeTotalCount
            ? (long)page * size < count
            : rows.Length > size;
        var items = rows.Length > size ? rows[..size] : rows;
        return new RuntimeInstanceCatalogPage(items, count, page, size, hasMore);
    }

    private static async Task<OrchestrationInstance[]> ReadSummaries(IQueryable<OrchestrationInstance> source, CancellationToken cancellationToken)
    {
        var rows = await source
            .Select(x => new
            {
                x.Id, x.OrchestrationDefinitionKey, x.RuntimeOrchestrationArtifactId,
                x.OrchestrationVersion, x.TotalTaskCount, x.CompletedTaskCount,
                x.CorrelationId, x.SagaId, x.ExecutionKey, x.Status,
                x.CurrentStageKey, x.CurrentTaskKey, x.LastExecutedStageKey, x.LastExecutedTaskKey,
                x.CurrentParallelGroupKey, x.StartedOnUtc, x.LastUpdatedOnUtc, x.WaitingSinceUtc,
                x.CompletedOnUtc, x.FailedOnUtc, x.ErrorSummary, x.FinalOutcome, x.StoppedOnUtc,
                x.CompensationStartedOnUtc, x.CompensatedOnUtc, x.RetryCount, x.Metadata
            }).ToArrayAsync(cancellationToken);
        return rows.Select(x => new OrchestrationInstance
        {
            Id = x.Id, OrchestrationDefinitionKey = x.OrchestrationDefinitionKey,
            RuntimeOrchestrationArtifactId = x.RuntimeOrchestrationArtifactId,
            OrchestrationVersion = x.OrchestrationVersion, TotalTaskCount = x.TotalTaskCount,
            CompletedTaskCount = x.CompletedTaskCount, CorrelationId = x.CorrelationId,
            SagaId = x.SagaId, ExecutionKey = x.ExecutionKey, Status = x.Status,
            CurrentStageKey = x.CurrentStageKey, CurrentTaskKey = x.CurrentTaskKey,
            LastExecutedStageKey = x.LastExecutedStageKey, LastExecutedTaskKey = x.LastExecutedTaskKey,
            CurrentParallelGroupKey = x.CurrentParallelGroupKey, StartedOnUtc = x.StartedOnUtc,
            LastUpdatedOnUtc = x.LastUpdatedOnUtc, WaitingSinceUtc = x.WaitingSinceUtc,
            CompletedOnUtc = x.CompletedOnUtc, FailedOnUtc = x.FailedOnUtc,
            ErrorSummary = x.ErrorSummary, FinalOutcome = x.FinalOutcome, StoppedOnUtc = x.StoppedOnUtc,
            CompensationStartedOnUtc = x.CompensationStartedOnUtc, CompensatedOnUtc = x.CompensatedOnUtc,
            RetryCount = x.RetryCount, Metadata = x.Metadata
        }).ToArray();
    }

    public async Task<IReadOnlyDictionary<OrchestrationInstanceStatus, long>> GetStatusCounts(
        RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = ApplyCatalogFilters(query);
        var durableCounts = await GetDurableStatusCounts(source, cancellationToken);
        if (_hotState?.IsEnabled != true) return durableCounts;
        var overlays = await GetPendingHotInstances(cancellationToken);
        if (overlays.Length == 0) return durableCounts;
        var durable = await ReadDurableInstances(overlays.Select(item => item.Id), cancellationToken);
        var byId = durable.ToDictionary(item => item.Id);
        var counts = durableCounts.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var overlay in overlays)
        {
            if (byId.TryGetValue(overlay.Id, out var previous) && MatchesCatalog(previous, query))
                AddCount(counts, previous.Status, -1);
            if (MatchesCatalog(overlay, query)) AddCount(counts, overlay.Status, 1);
        }
        return counts.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private async Task<IReadOnlyDictionary<OrchestrationInstanceStatus, long>> GetDurableStatusCounts(
        IQueryable<OrchestrationInstance> source,
        CancellationToken cancellationToken)
    {
        var provider = DbContext.Database.ProviderName ?? string.Empty;
        if (!ProvidersWithoutStatusGrouping.ContainsKey(provider))
        {
            try
            {
                return await source.GroupBy(x => x.Status)
                    .Select(group => new { Status = group.Key, Count = group.LongCount() })
                    .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("GroupBy", StringComparison.Ordinal)
                && exception.Message.Contains("could not be translated", StringComparison.OrdinalIgnoreCase))
            {
                ProvidersWithoutStatusGrouping.TryAdd(provider, true);
            }
        }

        // Some EF providers cannot translate GroupBy. Keep the work on the
        // provider with a fixed number of counts, never a query per instance.
        var counts = new Dictionary<OrchestrationInstanceStatus, long>();
        foreach (var status in Enum.GetValues<OrchestrationInstanceStatus>().Distinct())
        {
            var count = await source.LongCountAsync(item => item.Status == status, cancellationToken);
            if (count > 0) counts.Add(status, count);
        }
        return counts;
    }

    private async Task<OrchestrationInstance[]> GetPendingHotInstances(CancellationToken cancellationToken)
    {
        var records = await _hotState!.ReadPendingInstancesAsync(10_000, cancellationToken);
        return records.Select(RuntimeHotStateEntityCodec.Deserialize<OrchestrationInstance>)
            .Where(item => item is not null).Cast<OrchestrationInstance>().ToArray();
    }

    private async Task<long> GetLogicalCount(
        IQueryable<OrchestrationInstance> source,
        OrchestrationInstance[] overlays,
        RuntimeInstanceCatalogQuery query,
        CancellationToken cancellationToken)
    {
        var count = await source.LongCountAsync(cancellationToken);
        if (overlays.Length == 0) return count;
        var durable = await ReadDurableInstances(overlays.Select(item => item.Id), cancellationToken);
        var byId = durable.ToDictionary(item => item.Id);
        foreach (var overlay in overlays)
        {
            if (byId.TryGetValue(overlay.Id, out var previous) && MatchesCatalog(previous, query)) count--;
            if (MatchesCatalog(overlay, query)) count++;
        }
        return count;
    }

    private async Task<OrchestrationInstance[]> ReadDurableInstances(
        IEnumerable<Id> ids,
        CancellationToken cancellationToken)
    {
        var results = new List<OrchestrationInstance>();
        foreach (var chunk in ids.Distinct().Chunk(500))
            results.AddRange(await DbContext.OrchestrationInstances.AsNoTracking()
                .Where(item => chunk.Contains(item.Id)).ToArrayAsync(cancellationToken));
        return results.ToArray();
    }

    private static void AddCount(IDictionary<OrchestrationInstanceStatus, long> counts, OrchestrationInstanceStatus status, long delta)
        => counts[status] = (counts.TryGetValue(status, out var current) ? current : 0L) + delta;

    private static bool MatchesCatalog(OrchestrationInstance item, RuntimeInstanceCatalogQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var term = query.SearchText.Trim();
            if (!item.Id.ToString().Equals(term, StringComparison.OrdinalIgnoreCase) &&
                !(item.OrchestrationDefinitionKey?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !(item.CorrelationId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !(item.SagaId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !(item.ExecutionKey?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)) return false;
        }
        if (query.Status is { } status && item.Status != status) return false;
        if (query.Statuses is { Count: > 0 } && !query.Statuses.Contains(item.Status)) return false;
        if (!string.IsNullOrWhiteSpace(query.OrchestrationKey) &&
            !string.Equals(item.OrchestrationDefinitionKey, query.OrchestrationKey.Trim(), StringComparison.Ordinal)) return false;
        if (!string.IsNullOrWhiteSpace(query.OrchestrationVersion) &&
            !string.Equals(item.OrchestrationVersion, query.OrchestrationVersion.Trim(), StringComparison.Ordinal)) return false;
        if (query.StartedFromUtc is { } from && item.StartedOnUtc < from) return false;
        if (query.StartedBeforeUtc is { } before && item.StartedOnUtc >= before) return false;
        if (query.ActiveOnly && item.Status is not (OrchestrationInstanceStatus.Created or
            OrchestrationInstanceStatus.Running or OrchestrationInstanceStatus.Idle or
            OrchestrationInstanceStatus.Compensating)) return false;
        return true;
    }

    private static IOrderedQueryable<OrchestrationInstance> ApplyCatalogOrder(
        IQueryable<OrchestrationInstance> source,
        RuntimeInstanceCatalogQuery query)
    {
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
        return query.Descending ? ordered.ThenByDescending(x => x.Id) : ordered.ThenBy(x => x.Id);
    }

    private static IOrderedEnumerable<OrchestrationInstance> ApplyCatalogOrder(
        IEnumerable<OrchestrationInstance> source,
        RuntimeInstanceCatalogQuery query)
    {
        IOrderedEnumerable<OrchestrationInstance> ordered = (query.Sort, query.Descending) switch
        {
            (RuntimeInstanceCatalogSort.Started, false) => source.OrderBy(x => x.StartedOnUtc),
            (RuntimeInstanceCatalogSort.Started, true) => source.OrderByDescending(x => x.StartedOnUtc),
            (RuntimeInstanceCatalogSort.Orchestration, false) => source.OrderBy(x => x.OrchestrationDefinitionKey, StringComparer.Ordinal),
            (RuntimeInstanceCatalogSort.Orchestration, true) => source.OrderByDescending(x => x.OrchestrationDefinitionKey, StringComparer.Ordinal),
            (RuntimeInstanceCatalogSort.CorrelationId, false) => source.OrderBy(x => x.CorrelationId, StringComparer.Ordinal),
            (RuntimeInstanceCatalogSort.CorrelationId, true) => source.OrderByDescending(x => x.CorrelationId, StringComparer.Ordinal),
            (RuntimeInstanceCatalogSort.SagaId, false) => source.OrderBy(x => x.SagaId, StringComparer.Ordinal),
            (RuntimeInstanceCatalogSort.SagaId, true) => source.OrderByDescending(x => x.SagaId, StringComparer.Ordinal),
            (RuntimeInstanceCatalogSort.Status, false) => source.OrderBy(x => x.Status),
            (RuntimeInstanceCatalogSort.Status, true) => source.OrderByDescending(x => x.Status),
            (_, false) => source.OrderBy(x => x.LastUpdatedOnUtc),
            _ => source.OrderByDescending(x => x.LastUpdatedOnUtc)
        };
        return query.Descending ? ordered.ThenByDescending(x => x.Id) : ordered.ThenBy(x => x.Id);
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
        if (query.Statuses is { Count: > 0 }) source = source.Where(x => query.Statuses.Contains(x.Status));
        if (!string.IsNullOrWhiteSpace(query.OrchestrationKey))
        {
            var key = query.OrchestrationKey.Trim();
            source = source.Where(x => x.OrchestrationDefinitionKey == key);
        }
        if (!string.IsNullOrWhiteSpace(query.OrchestrationVersion))
        {
            var version = query.OrchestrationVersion.Trim();
            source = source.Where(x => x.OrchestrationVersion == version);
        }
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

    public async Task<IReadOnlyCollection<Id>> GetProgressRecoveryCandidates(
        DateTime startedOnOrBeforeUtc,
        DateTime leaseExpiredOnOrBeforeUtc,
        int take = 100,
        CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(instance =>
                instance.Status == OrchestrationInstanceStatus.Running &&
                instance.StartedOnUtc <= startedOnOrBeforeUtc &&
                (instance.ActiveLeaseExpiresOnUtc == null || instance.ActiveLeaseExpiresOnUtc <= leaseExpiredOnOrBeforeUtc) &&
                !DbContext.TaskExecutions.Any(task =>
                    task.OrchestrationInstanceId == instance.Id &&
                    (task.Status == TaskExecutionStatus.Pending ||
                     task.Status == TaskExecutionStatus.Running ||
                     task.Status == TaskExecutionStatus.WaitingResponse ||
                     task.Status == TaskExecutionStatus.Retrying)))
            .OrderBy(instance => instance.StartedOnUtc)
            .ThenBy(instance => instance.Id)
            .Select(instance => instance.Id)
            .Take(Math.Max(1, take))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Id>> GetLifetimeCandidates(
        Id artifactId, DateTime startedOnOrBeforeUtc, int skip, int take, CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.RuntimeOrchestrationArtifactId == artifactId &&
                x.Status == OrchestrationInstanceStatus.Running && x.StartedOnUtc <= startedOnOrBeforeUtc)
            .OrderBy(x => x.StartedOnUtc).ThenBy(x => x.Id)
            .Skip(Math.Max(0, skip)).Take(Math.Max(1, take))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Id>> GetExpiredInstances(DateTime utcNow, int skip, int take, CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.Status == OrchestrationInstanceStatus.Running && x.ExpiresOnUtc != null && x.ExpiresOnUtc <= utcNow)
            .OrderBy(x => x.ExpiresOnUtc).ThenBy(x => x.Id)
            .Skip(Math.Max(0, skip)).Take(Math.Max(1, take)).Select(x => x.Id).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Id>> GetUnprojectedInstances(int skip, int take, CancellationToken cancellationToken = default)
        => await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.MaxInstanceDurationSeconds == null &&
                (x.Status == OrchestrationInstanceStatus.Created || x.Status == OrchestrationInstanceStatus.Running ||
                 x.Status == OrchestrationInstanceStatus.Idle || x.Status == OrchestrationInstanceStatus.Failed ||
                 x.Status == OrchestrationInstanceStatus.DeadLettered))
            .OrderBy(x => x.StartedOnUtc).ThenBy(x => x.Id)
            .Skip(Math.Max(0, skip)).Take(Math.Max(1, take)).Select(x => x.Id).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<OrchestrationLifetimeCandidate>> GetLifetimeCandidatePage(
        DateTime startedOnOrBeforeUtc, int skip, int take, CancellationToken cancellationToken = default)
    {
        var rows = await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.Status == OrchestrationInstanceStatus.Running && x.StartedOnUtc <= startedOnOrBeforeUtc)
            .OrderBy(x => x.StartedOnUtc).ThenBy(x => x.Id)
            .Skip(Math.Max(0, skip)).Take(Math.Max(1, take))
            .Select(x => new { x.Id, x.RuntimeOrchestrationArtifactId, x.StartedOnUtc })
            .ToArrayAsync(cancellationToken);
        return rows.Select(x => new OrchestrationLifetimeCandidate(x.Id, x.RuntimeOrchestrationArtifactId, x.StartedOnUtc)).ToArray();
    }

    public Task<bool> HasActiveInstances(Id artifactId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return DbContext.OrchestrationInstances.AsNoTracking().AnyAsync(x => x.RuntimeOrchestrationArtifactId == artifactId &&
            ((x.Status != OrchestrationInstanceStatus.Stopped && x.Status != OrchestrationInstanceStatus.Compensated &&
              x.Status != OrchestrationInstanceStatus.Completed && x.Status != OrchestrationInstanceStatus.CompletedWithErrors &&
              x.Status != OrchestrationInstanceStatus.Failed && x.Status != OrchestrationInstanceStatus.DeadLettered &&
              x.Status != OrchestrationInstanceStatus.Aborted) ||
             (x.ActiveLeaseId != null && x.ActiveLeaseId != "" && x.ActiveLeaseExpiresOnUtc > now)), cancellationToken);
    }

    public async Task<IReadOnlyCollection<Id>> GetActiveArtifactIds(IReadOnlyCollection<Id> artifactIds, CancellationToken cancellationToken = default)
    {
        if (artifactIds is null || artifactIds.Count == 0) return [];
        var now = DateTime.UtcNow;
        return await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => artifactIds.Contains(x.RuntimeOrchestrationArtifactId) &&
                ((x.Status != OrchestrationInstanceStatus.Stopped && x.Status != OrchestrationInstanceStatus.Compensated &&
                  x.Status != OrchestrationInstanceStatus.Completed && x.Status != OrchestrationInstanceStatus.CompletedWithErrors &&
                  x.Status != OrchestrationInstanceStatus.Failed && x.Status != OrchestrationInstanceStatus.DeadLettered &&
                  x.Status != OrchestrationInstanceStatus.Aborted) ||
                 (x.ActiveLeaseId != null && x.ActiveLeaseId != "" && x.ActiveLeaseExpiresOnUtc > now)))
            .Select(x => x.RuntimeOrchestrationArtifactId).Distinct().ToArrayAsync(cancellationToken);
    }

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
