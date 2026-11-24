using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.HotState;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

/// <summary>
/// Reads dashboard statistics from the canonical execution state when deferred
/// statistics observations are intentionally disabled. This keeps observability off
/// the mutation path without leaving the dashboard empty.
/// </summary>
internal sealed class OperationalSagaStatisticsReader(
    RuntimeDbContext db,
    IRuntimeHotStateStore hotState)
    : ISagaStatisticsReader, ISagaStatisticsCohortActivityReader
{
    public async Task<SagaStatisticsReport> GetReportAsync(
        DateTime fromUtc,
        DateTime beforeUtc,
        string orchestrationKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = new SagaStatisticsDashboardQuery(fromUtc, beforeUtc, orchestrationKey).Normalize();
        var overlay = await ReadOverlay(cancellationToken);
        var orchestrationCounts = await FilterInstances(query)
            .GroupBy(instance => instance.OrchestrationDefinitionKey)
            .Select(group => new { Key = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
        var statuses = await FilterInstances(query).GroupBy(instance => instance.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        ApplyStatusOverlay(statuses, overlay, instance => MatchesCohort(instance, query));
        ApplyKeyOverlay(orchestrationCounts, overlay, instance => MatchesCohort(instance, query));
        var activity = (await GetActivityAsync(query, SagaStatisticsActivityBucket.Day, cancellationToken))
            .Select(item => new SagaStatisticsActivity(item.BucketUtc, item.Status, item.Count))
            .ToArray();
        var reached = Enum.GetValues<OrchestrationInstanceStatus>()
            .ToDictionary(status => status, status => statuses.GetValueOrDefault(status));
        var lastObserved = await FilterInstances(query).Select(instance => (DateTime?)instance.LastUpdatedOnUtc)
            .MaxAsync(cancellationToken);
        lastObserved = Max(lastObserved, overlay.Hot.Where(instance => MatchesCohort(instance, query))
            .Select(instance => (DateTime?)instance.LastUpdatedOnUtc).DefaultIfEmpty().Max());

        return new SagaStatisticsReport(
            query.FromUtc,
            query.BeforeUtc,
            query.OrchestrationKey,
            statuses.Values.Sum(),
            statuses,
            reached,
            activity,
            orchestrationCounts.Where(item => item.Value > 0).OrderBy(item => item.Key)
                .Select(item => new SagaStatisticsOrchestration(item.Key, item.Value)).ToArray(),
            overlay.Hot.Length,
            true,
            lastObserved);
    }

    public async Task<IReadOnlyCollection<string>> GetOrchestrationKeysAsync(
        CancellationToken cancellationToken = default)
    {
        var durable = await db.OrchestrationInstances.AsNoTracking()
            .Select(instance => instance.OrchestrationDefinitionKey)
            .Distinct()
            .OrderBy(key => key)
            .ToArrayAsync(cancellationToken);
        var overlay = await ReadOverlay(cancellationToken);
        return durable.Concat(overlay.Hot.Select(instance => instance.OrchestrationDefinitionKey))
            .Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }

    public async Task<IReadOnlyCollection<string>> SearchOrchestrationKeysAsync(
        string search,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var normalized = search?.Trim() ?? string.Empty;
        var limit = Math.Clamp(take, 1, 50);
        var keys = db.OrchestrationInstances.AsNoTracking()
            .Select(instance => instance.OrchestrationDefinitionKey)
            .Distinct();
        if (!string.IsNullOrEmpty(normalized))
            keys = keys.Where(key => EF.Functions.Like(key, $"%{normalized}%"));

        var durable = await keys.OrderBy(key => key).Take(limit).ToArrayAsync(cancellationToken);
        var overlay = await ReadOverlay(cancellationToken);
        return durable.Concat(overlay.Hot.Select(instance => instance.OrchestrationDefinitionKey))
            .Where(key => !string.IsNullOrWhiteSpace(key) &&
                (string.IsNullOrEmpty(normalized) || key.Contains(normalized, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).Take(limit).ToArray();
    }

    public async Task<IReadOnlyCollection<string>> SearchOrchestrationVersionsAsync(
        string orchestrationKey,
        string search,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orchestrationKey)) return Array.Empty<string>();

        var normalized = search?.Trim() ?? string.Empty;
        var versions = await db.OrchestrationInstances.AsNoTracking()
            .Where(instance => instance.OrchestrationDefinitionKey == orchestrationKey.Trim() &&
                instance.OrchestrationVersion != null)
            .Select(instance => instance.OrchestrationVersion)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var overlay = await ReadOverlay(cancellationToken);
        return versions.Concat(overlay.Hot.Where(instance =>
                string.Equals(instance.OrchestrationDefinitionKey, orchestrationKey.Trim(), StringComparison.Ordinal))
            .Select(instance => instance.OrchestrationVersion))
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Where(version => string.IsNullOrEmpty(normalized) ||
                version.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(version => version, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(take, 1, 50))
            .ToArray();
    }

    public async Task<SagaStatisticsDashboardCohort> GetCohortAsync(
        SagaStatisticsDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query = query.Normalize();
        var overlay = await ReadOverlay(cancellationToken);
        var statuses = await FilterInstances(query)
            .GroupBy(instance => instance.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        ApplyStatusOverlay(statuses, overlay, instance => MatchesCohort(instance, query));
        return new SagaStatisticsDashboardCohort(
            query.FromUtc,
            query.BeforeUtc,
            query.OrchestrationKey,
            statuses.Values.Sum(),
            statuses);
    }

    public async Task<IReadOnlyCollection<SagaStatisticsDashboardActivity>> GetActivityAsync(
        SagaStatisticsDashboardQuery query,
        SagaStatisticsActivityBucket bucket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query = query.Normalize();
        if (!Enum.IsDefined(bucket)) throw new ArgumentOutOfRangeException(nameof(bucket));

        var selected = ScopeInstances(query);
        if (query.RestrictActivityToStartedCohort)
        {
            selected = selected.Where(instance =>
                instance.StartedOnUtc >= query.FromUtc && instance.StartedOnUtc < query.BeforeUtc);
        }
        else
        {
            selected = selected.Where(instance =>
                (instance.CompletedOnUtc >= query.FromUtc && instance.CompletedOnUtc < query.BeforeUtc) ||
                (instance.FailedOnUtc >= query.FromUtc && instance.FailedOnUtc < query.BeforeUtc) ||
                (instance.StoppedOnUtc >= query.FromUtc && instance.StoppedOnUtc < query.BeforeUtc) ||
                (instance.CompensatedOnUtc >= query.FromUtc && instance.CompensatedOnUtc < query.BeforeUtc));
        }

        var terminals = await selected.Select(instance => new
            {
                instance.Id,
                instance.Status,
                instance.CompletedOnUtc,
                instance.FailedOnUtc,
                instance.StoppedOnUtc,
                instance.CompensatedOnUtc
            })
            .ToArrayAsync(cancellationToken);

        var counts = terminals
            .Select(item => new { item.Status, OccurredOnUtc = TerminalOnUtc(item.Status, item.CompletedOnUtc,
                item.FailedOnUtc, item.StoppedOnUtc, item.CompensatedOnUtc) })
            .Where(item => item.OccurredOnUtc >= query.FromUtc && item.OccurredOnUtc < query.BeforeUtc)
            .GroupBy(item => new { BucketUtc = BucketStart(item.OccurredOnUtc!.Value, bucket), item.Status })
            .ToDictionary(group => (group.Key.BucketUtc, group.Key.Status), group => group.LongCount());

        var overlay = await ReadOverlay(cancellationToken);
        foreach (var previous in overlay.Durable)
            ApplyActivityDelta(counts, previous, query, bucket, -1);
        foreach (var current in overlay.Hot)
            ApplyActivityDelta(counts, current, query, bucket, 1);
        return counts.Where(item => item.Value > 0)
            .OrderBy(item => item.Key.BucketUtc).ThenBy(item => item.Key.Status)
            .Select(item => new SagaStatisticsDashboardActivity(item.Key.BucketUtc, item.Key.Status, item.Value))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<SagaStatisticsDashboardOrchestration>> GetTopOrchestrationsAsync(
        SagaStatisticsDashboardQuery query,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take));
        query = query.Normalize();
        var rankings = await FilterInstances(query)
            .GroupBy(instance => instance.OrchestrationDefinitionKey)
            .Select(group => new { OrchestrationKey = group.Key, StartedInstances = group.LongCount() })
            .ToDictionaryAsync(item => item.OrchestrationKey, item => item.StartedInstances, cancellationToken);
        var overlay = await ReadOverlay(cancellationToken);
        ApplyKeyOverlay(rankings, overlay, instance => MatchesCohort(instance, query));
        return rankings.Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value).ThenBy(item => item.Key).Take(take)
            .Select(item => new SagaStatisticsDashboardOrchestration(item.Key, item.Value)).ToArray();
    }

    public async Task<SagaStatisticsDashboardHealth> GetHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var overlay = await ReadOverlay(cancellationToken);
        var lastObserved = await db.OrchestrationInstances.AsNoTracking()
            .Select(instance => (DateTime?)instance.LastUpdatedOnUtc)
            .MaxAsync(cancellationToken);
        lastObserved = Max(lastObserved, overlay.Hot.Select(instance => (DateTime?)instance.LastUpdatedOnUtc)
            .DefaultIfEmpty().Max());
        return new SagaStatisticsDashboardHealth(overlay.Hot.Length, true, lastObserved);
    }

    private IQueryable<OrchestrationInstance> FilterInstances(SagaStatisticsDashboardQuery query)
        => ScopeInstances(query).Where(instance =>
            instance.StartedOnUtc >= query.FromUtc && instance.StartedOnUtc < query.BeforeUtc);

    private IQueryable<OrchestrationInstance> ScopeInstances(SagaStatisticsDashboardQuery query)
    {
        var selected = db.OrchestrationInstances.AsNoTracking();
        if (query.OrchestrationKey is not null)
            selected = selected.Where(instance => instance.OrchestrationDefinitionKey == query.OrchestrationKey);
        if (query.OrchestrationVersion is not null)
            selected = selected.Where(instance => instance.OrchestrationVersion == query.OrchestrationVersion);
        return selected;
    }

    private async Task<InstanceOverlay> ReadOverlay(CancellationToken cancellationToken)
    {
        if (hotState?.IsEnabled != true) return InstanceOverlay.Empty;
        var records = await hotState.ReadPendingInstancesAsync(100_000, cancellationToken);
        var current = records.Select(RuntimeHotStateEntityCodec.Deserialize<OrchestrationInstance>)
            .Where(instance => instance is not null).Cast<OrchestrationInstance>().ToArray();
        if (current.Length == 0) return InstanceOverlay.Empty;

        var durable = new List<OrchestrationInstance>(current.Length);
        foreach (var ids in current.Select(instance => instance.Id).Distinct().Chunk(500))
            durable.AddRange(await db.OrchestrationInstances.AsNoTracking()
                .Where(instance => ids.Contains(instance.Id)).ToArrayAsync(cancellationToken));
        return new InstanceOverlay(current, durable.ToArray());
    }

    private static void ApplyStatusOverlay(
        IDictionary<OrchestrationInstanceStatus, long> counts,
        InstanceOverlay overlay,
        Func<OrchestrationInstance, bool> predicate)
    {
        foreach (var previous in overlay.Durable.Where(predicate)) Add(counts, previous.Status, -1);
        foreach (var current in overlay.Hot.Where(predicate)) Add(counts, current.Status, 1);
        RemoveEmpty(counts);
    }

    private static void ApplyKeyOverlay(
        IDictionary<string, long> counts,
        InstanceOverlay overlay,
        Func<OrchestrationInstance, bool> predicate)
    {
        foreach (var previous in overlay.Durable.Where(predicate)) Add(counts, previous.OrchestrationDefinitionKey, -1);
        foreach (var current in overlay.Hot.Where(predicate)) Add(counts, current.OrchestrationDefinitionKey, 1);
        RemoveEmpty(counts);
    }

    private static void ApplyActivityDelta(
        IDictionary<(DateTime BucketUtc, OrchestrationInstanceStatus Status), long> counts,
        OrchestrationInstance instance,
        SagaStatisticsDashboardQuery query,
        SagaStatisticsActivityBucket bucket,
        long delta)
    {
        if (!MatchesScope(instance, query) ||
            (query.RestrictActivityToStartedCohort && !MatchesCohort(instance, query))) return;
        var occurredOnUtc = TerminalOnUtc(instance.Status, instance.CompletedOnUtc, instance.FailedOnUtc,
            instance.StoppedOnUtc, instance.CompensatedOnUtc);
        if (occurredOnUtc is null || occurredOnUtc < query.FromUtc || occurredOnUtc >= query.BeforeUtc) return;
        Add(counts, (BucketStart(occurredOnUtc.Value, bucket), instance.Status), delta);
    }

    private static bool MatchesCohort(OrchestrationInstance instance, SagaStatisticsDashboardQuery query)
        => MatchesScope(instance, query) && instance.StartedOnUtc >= query.FromUtc && instance.StartedOnUtc < query.BeforeUtc;

    private static bool MatchesScope(OrchestrationInstance instance, SagaStatisticsDashboardQuery query)
        => (query.OrchestrationKey is null || string.Equals(instance.OrchestrationDefinitionKey, query.OrchestrationKey, StringComparison.Ordinal)) &&
           (query.OrchestrationVersion is null || string.Equals(instance.OrchestrationVersion, query.OrchestrationVersion, StringComparison.Ordinal));

    private static void Add<TKey>(IDictionary<TKey, long> counts, TKey key, long delta) where TKey : notnull
        => counts[key] = counts.TryGetValue(key, out var current) ? current + delta : delta;

    private static void RemoveEmpty<TKey>(IDictionary<TKey, long> counts) where TKey : notnull
    {
        foreach (var key in counts.Where(item => item.Value <= 0).Select(item => item.Key).ToArray()) counts.Remove(key);
    }

    private static DateTime? Max(DateTime? left, DateTime? right)
        => left is null ? right : right is null ? left : left >= right ? left : right;

    private sealed record InstanceOverlay(OrchestrationInstance[] Hot, OrchestrationInstance[] Durable)
    {
        public static InstanceOverlay Empty { get; } = new([], []);
    }

    private static DateTime? TerminalOnUtc(
        OrchestrationInstanceStatus status,
        DateTime? completedOnUtc,
        DateTime? failedOnUtc,
        DateTime? stoppedOnUtc,
        DateTime? compensatedOnUtc)
        => status switch
        {
            OrchestrationInstanceStatus.Completed or OrchestrationInstanceStatus.CompletedWithErrors => completedOnUtc,
            OrchestrationInstanceStatus.Compensated => compensatedOnUtc,
            OrchestrationInstanceStatus.Stopped or OrchestrationInstanceStatus.Aborted => stoppedOnUtc,
            OrchestrationInstanceStatus.DeadLettered or OrchestrationInstanceStatus.Failed => failedOnUtc,
            _ => null
        };

    private static DateTime BucketStart(DateTime value, SagaStatisticsActivityBucket bucket)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return bucket switch
        {
            SagaStatisticsActivityBucket.Minute => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc),
            SagaStatisticsActivityBucket.Hour => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc),
            SagaStatisticsActivityBucket.Day => utc.Date,
            SagaStatisticsActivityBucket.Week => utc.Date.AddDays(-((7 + (int)utc.DayOfWeek - (int)DayOfWeek.Monday) % 7)),
            _ => throw new ArgumentOutOfRangeException(nameof(bucket))
        };
    }
}
