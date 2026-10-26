using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class SagaStatisticsBatchProcessor(RuntimeDbContext db, ISagaStatisticsProjector projector)
    : ISagaStatisticsBatchProcessor
{
    public async Task<SagaStatisticsBatchResult> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var ids = await db.SagaStatisticsObservations.AsNoTracking().Where(x => !x.Processed)
            .OrderBy(x => x.InstanceId).ThenBy(x => x.Revision).Take(batchSize)
            .Select(x => x.Id).ToArrayAsync(cancellationToken);
        var applied = 0;
        var deferred = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observation = await db.SagaStatisticsObservations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (observation is null || observation.Processed) continue;
            var instanceId = observation.InstanceId.ToString();
            var projection = await db.SagaStatisticsProjections.FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
            var snapshot = projection is null ? null : new SagaStatisticsSnapshot(projection.Id, projection.OrchestrationKey,
                projection.ArtifactId.ToString(), projection.Revision, projection.Status, projection.StartedOnUtc, projection.ReachedStatuses);
            var entry = new SagaStatisticsEvent(instanceId, observation.OrchestrationKey, observation.ArtifactId.ToString(),
                observation.Revision, observation.Status, observation.StartedOnUtc, observation.OccurredOnUtc);
            var result = projector.Project(snapshot, entry);
            if (result.Disposition == SagaStatisticsProjectionDisposition.Gap)
            {
                deferred++;
                db.ChangeTracker.Clear();
                continue;
            }

            if (result.Disposition == SagaStatisticsProjectionDisposition.Applied)
            {
                if (projection is null)
                {
                    projection = new SagaStatisticsProjection { Id = instanceId, ArtifactId = observation.ArtifactId,
                        OrchestrationKey = observation.OrchestrationKey, StartedOnUtc = observation.StartedOnUtc };
                    db.SagaStatisticsProjections.Add(projection);
                }
                projection.Revision = result.Snapshot.Revision;
                projection.Status = result.Snapshot.Status;
                projection.ReachedStatuses = result.Snapshot.ReachedStatuses;
                projection.LastObservedOnUtc = observation.OccurredOnUtc;
                observation.StatusChanged = result.StatusChanged;
                observation.StatusFirstReached = result.NewlyReachedStatus is not null;
            }
            observation.Processed = true;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                applied++;
            }
            catch (DbUpdateConcurrencyException)
            {
                deferred++;
            }
            finally { db.ChangeTracker.Clear(); }
        }
        return new SagaStatisticsBatchResult(ids.Length, applied, deferred);
    }
}
