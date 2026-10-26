using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class SagaStatisticsReader(RuntimeDbContext db) : ISagaStatisticsReader, ISagaStatisticsDashboardReader
{
    public async Task<SagaStatisticsReport> GetReportAsync(DateTime fromUtc, DateTime beforeUtc, string orchestrationKey = null,
        CancellationToken cancellationToken = default)
    {
        if (fromUtc >= beforeUtc) throw new ArgumentException("The report start must precede its end.");
        var all = db.SagaStatisticsProjections.AsNoTracking().Where(x => x.StartedOnUtc >= fromUtc && x.StartedOnUtc < beforeUtc);
        var orchestrationCounts = await all.GroupBy(x => x.OrchestrationKey).OrderBy(group => group.Key)
            .Select(group => new { Key = group.Key, Count = group.LongCount() }).ToArrayAsync(cancellationToken);
        var orchestrations = orchestrationCounts.Select(x => new SagaStatisticsOrchestration(x.Key, x.Count)).ToArray();
        var selected = string.IsNullOrWhiteSpace(orchestrationKey) ? all : all.Where(x => x.OrchestrationKey == orchestrationKey);
        var statuses = await selected.GroupBy(x => x.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        var reachedGroups = await selected.GroupBy(x => x.ReachedStatuses)
            .Select(group => new { Flags = group.Key, Count = group.LongCount() }).ToArrayAsync(cancellationToken);
        var reached = Enum.GetValues<OrchestrationInstanceStatus>().ToDictionary(status => status,
            status => reachedGroups.Where(group => (group.Flags & (1L << (int)status)) != 0).Sum(group => group.Count));

        var observations = db.SagaStatisticsObservations.AsNoTracking().Where(x => x.Processed && x.StatusChanged && !x.IsBackfill
            && x.OccurredOnUtc >= fromUtc && x.OccurredOnUtc < beforeUtc);
        if (!string.IsNullOrWhiteSpace(orchestrationKey)) observations = observations.Where(x => x.OrchestrationKey == orchestrationKey);
        var activityCounts = await observations.GroupBy(x => new { Day = x.OccurredOnUtc.Date, x.Status })
            .OrderBy(group => group.Key.Day)
            .Select(group => new { group.Key.Day, group.Key.Status, Count = group.LongCount() }).ToArrayAsync(cancellationToken);
        var activity = activityCounts.Select(x => new SagaStatisticsActivity(x.Day, x.Status, x.Count)).ToArray();
        var lastObserved = await selected.Select(x => (DateTime?)x.LastObservedOnUtc).MaxAsync(cancellationToken);
        var pending = await db.SagaStatisticsObservations.AsNoTracking().LongCountAsync(x => !x.Processed, cancellationToken);
        var backfillComplete = await db.SagaStatisticsBackfillCheckpoints.AsNoTracking().AnyAsync(cancellationToken);
        return new SagaStatisticsReport(fromUtc, beforeUtc, orchestrationKey, statuses.Values.Sum(), statuses, reached,
            activity, orchestrations, pending, backfillComplete, lastObserved);
    }

    public async Task<IReadOnlyCollection<string>> GetOrchestrationKeysAsync(CancellationToken cancellationToken = default)
        => await db.SagaStatisticsProjections.AsNoTracking()
            .Select(item => item.OrchestrationKey)
            .Distinct()
            .OrderBy(key => key)
            .ToArrayAsync(cancellationToken);

    public async Task<SagaStatisticsDashboardCohort> GetCohortAsync(SagaStatisticsDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query = query.Normalize();
        var selected = FilterProjections(query);
        var statuses = await selected.GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        return new SagaStatisticsDashboardCohort(query.FromUtc, query.BeforeUtc, query.OrchestrationKey,
            statuses.Values.Sum(), statuses);
    }

    public async Task<IReadOnlyCollection<SagaStatisticsDashboardActivity>> GetActivityAsync(SagaStatisticsDashboardQuery query,
        SagaStatisticsActivityBucket bucket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query = query.Normalize();
        if (!Enum.IsDefined(bucket))
        {
            throw new ArgumentOutOfRangeException(nameof(bucket));
        }

        var observations = db.SagaStatisticsObservations.AsNoTracking()
            .Where(item => item.Processed && item.StatusChanged && !item.IsBackfill
                && item.OccurredOnUtc >= query.FromUtc && item.OccurredOnUtc < query.BeforeUtc);
        if (query.OrchestrationKey is not null)
        {
            observations = observations.Where(item => item.OrchestrationKey == query.OrchestrationKey);
        }

        if (bucket == SagaStatisticsActivityBucket.Hour)
        {
            var hourly = await observations.GroupBy(item => new
                { item.OccurredOnUtc.Year, item.OccurredOnUtc.Month, item.OccurredOnUtc.Day, item.OccurredOnUtc.Hour, item.Status })
                .OrderBy(group => group.Key.Year).ThenBy(group => group.Key.Month).ThenBy(group => group.Key.Day).ThenBy(group => group.Key.Hour)
                .Select(group => new { group.Key.Year, group.Key.Month, group.Key.Day, group.Key.Hour, group.Key.Status, Count = group.LongCount() })
                .ToArrayAsync(cancellationToken);
            return hourly.Select(item => new SagaStatisticsDashboardActivity(
                new DateTime(item.Year, item.Month, item.Day, item.Hour, 0, 0, DateTimeKind.Utc), item.Status, item.Count)).ToArray();
        }

        var daily = await observations.GroupBy(item => new { Day = item.OccurredOnUtc.Date, item.Status })
            .OrderBy(group => group.Key.Day).ThenBy(group => group.Key.Status)
            .Select(group => new { group.Key.Day, group.Key.Status, Count = group.LongCount() })
            .ToArrayAsync(cancellationToken);
        if (bucket == SagaStatisticsActivityBucket.Day)
        {
            return daily.Select(item => new SagaStatisticsDashboardActivity(item.Day, item.Status, item.Count)).ToArray();
        }

        return daily.GroupBy(item => new { BucketUtc = WeekStart(item.Day), item.Status })
            .OrderBy(group => group.Key.BucketUtc).ThenBy(group => group.Key.Status)
            .Select(group => new SagaStatisticsDashboardActivity(group.Key.BucketUtc, group.Key.Status, group.Sum(item => item.Count)))
            .ToArray();
    }

    public async Task<SagaStatisticsDashboardHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db.SagaStatisticsObservations.AsNoTracking()
            .LongCountAsync(item => !item.Processed, cancellationToken);
        var backfillCompleted = await db.SagaStatisticsBackfillCheckpoints.AsNoTracking()
            .AnyAsync(cancellationToken);
        var lastObserved = await db.SagaStatisticsProjections.AsNoTracking()
            .Select(item => (DateTime?)item.LastObservedOnUtc)
            .MaxAsync(cancellationToken);
        return new SagaStatisticsDashboardHealth(pending, backfillCompleted, lastObserved);
    }

    private IQueryable<SagaStatisticsProjection> FilterProjections(SagaStatisticsDashboardQuery query)
    {
        var selected = db.SagaStatisticsProjections.AsNoTracking()
            .Where(item => item.StartedOnUtc >= query.FromUtc && item.StartedOnUtc < query.BeforeUtc);
        return query.OrchestrationKey is null
            ? selected
            : selected.Where(item => item.OrchestrationKey == query.OrchestrationKey);
    }

    private static DateTime WeekStart(DateTime occurredOnUtc)
    {
        // Runtime statistics persist their timestamps as UTC; SQLite materializes them as Unspecified.
        var utc = DateTime.SpecifyKind(occurredOnUtc, DateTimeKind.Utc);
        return utc.Date.AddDays(-((7 + (int)utc.DayOfWeek - (int)DayOfWeek.Monday) % 7));
    }
}
