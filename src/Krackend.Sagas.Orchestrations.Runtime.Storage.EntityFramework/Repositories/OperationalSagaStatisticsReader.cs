using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

/// <summary>
/// Reads dashboard statistics from the canonical execution state when deferred
/// statistics observations are intentionally disabled. This keeps observability off
/// the mutation path without leaving the dashboard empty.
/// </summary>
internal sealed class OperationalSagaStatisticsReader(RuntimeDbContext db)
    : ISagaStatisticsReader, ISagaStatisticsCohortActivityReader
{
    public async Task<SagaStatisticsReport> GetReportAsync(
        DateTime fromUtc,
        DateTime beforeUtc,
        string orchestrationKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = new SagaStatisticsDashboardQuery(fromUtc, beforeUtc, orchestrationKey).Normalize();
        var selected = FilterInstances(query);
        var orchestrationCounts = await db.OrchestrationInstances.AsNoTracking()
            .Where(instance => instance.StartedOnUtc >= query.FromUtc && instance.StartedOnUtc < query.BeforeUtc)
            .GroupBy(instance => instance.OrchestrationDefinitionKey)
            .Select(group => new { Key = group.Key, Count = group.LongCount() })
            .OrderBy(item => item.Key)
            .ToArrayAsync(cancellationToken);
        var statuses = await selected.GroupBy(instance => instance.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        var activity = (await GetActivityAsync(query, SagaStatisticsActivityBucket.Day, cancellationToken))
            .Select(item => new SagaStatisticsActivity(item.BucketUtc, item.Status, item.Count))
            .ToArray();
        var reached = Enum.GetValues<OrchestrationInstanceStatus>()
            .ToDictionary(status => status, status => statuses.GetValueOrDefault(status));
        var lastObserved = await selected.Select(instance => (DateTime?)instance.LastUpdatedOnUtc)
            .MaxAsync(cancellationToken);

        return new SagaStatisticsReport(
            query.FromUtc,
            query.BeforeUtc,
            query.OrchestrationKey,
            statuses.Values.Sum(),
            statuses,
            reached,
            activity,
            orchestrationCounts.Select(item => new SagaStatisticsOrchestration(item.Key, item.Count)).ToArray(),
            0,
            true,
            lastObserved);
    }

    public async Task<IReadOnlyCollection<string>> GetOrchestrationKeysAsync(
        CancellationToken cancellationToken = default)
        => await db.OrchestrationInstances.AsNoTracking()
            .Select(instance => instance.OrchestrationDefinitionKey)
            .Distinct()
            .OrderBy(key => key)
            .ToArrayAsync(cancellationToken);

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

        return await keys.OrderBy(key => key).Take(limit).ToArrayAsync(cancellationToken);
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
        return versions
            .Where(version => string.IsNullOrEmpty(normalized) ||
                version.Contains(normalized, StringComparison.OrdinalIgnoreCase))
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
        var statuses = await FilterInstances(query)
            .GroupBy(instance => instance.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
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
                instance.Status,
                instance.CompletedOnUtc,
                instance.FailedOnUtc,
                instance.StoppedOnUtc,
                instance.CompensatedOnUtc
            })
            .ToArrayAsync(cancellationToken);

        return terminals
            .Select(item => new { item.Status, OccurredOnUtc = TerminalOnUtc(item.Status, item.CompletedOnUtc,
                item.FailedOnUtc, item.StoppedOnUtc, item.CompensatedOnUtc) })
            .Where(item => item.OccurredOnUtc >= query.FromUtc && item.OccurredOnUtc < query.BeforeUtc)
            .GroupBy(item => new { BucketUtc = BucketStart(item.OccurredOnUtc!.Value, bucket), item.Status })
            .OrderBy(group => group.Key.BucketUtc)
            .ThenBy(group => group.Key.Status)
            .Select(group => new SagaStatisticsDashboardActivity(group.Key.BucketUtc, group.Key.Status, group.LongCount()))
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
            .OrderByDescending(item => item.StartedInstances)
            .ThenBy(item => item.OrchestrationKey)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return rankings.Select(item =>
            new SagaStatisticsDashboardOrchestration(item.OrchestrationKey, item.StartedInstances)).ToArray();
    }

    public async Task<SagaStatisticsDashboardHealth> GetHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var lastObserved = await db.OrchestrationInstances.AsNoTracking()
            .Select(instance => (DateTime?)instance.LastUpdatedOnUtc)
            .MaxAsync(cancellationToken);
        return new SagaStatisticsDashboardHealth(0, true, lastObserved);
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
