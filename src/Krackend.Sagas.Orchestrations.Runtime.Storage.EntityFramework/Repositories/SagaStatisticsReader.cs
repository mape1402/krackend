using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class SagaStatisticsReader(RuntimeDbContext db) : ISagaStatisticsReader
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
}
