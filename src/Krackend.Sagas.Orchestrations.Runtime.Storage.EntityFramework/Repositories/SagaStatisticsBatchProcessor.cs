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
        var observations = await db.SagaStatisticsObservations.Where(x => !x.Processed)
            .OrderBy(x => x.InstanceId).ThenBy(x => x.Revision).Take(batchSize)
            .ToArrayAsync(cancellationToken);
        if (observations.Length == 0)
            return new SagaStatisticsBatchResult(0, 0, 0);

        var instanceIds = observations.Select(x => x.InstanceId.ToString()).Distinct().ToArray();
        var projections = (await db.SagaStatisticsProjections
                .Where(x => instanceIds.Contains(x.Id))
                .ToArrayAsync(cancellationToken))
            .ToDictionary(x => x.Id, StringComparer.Ordinal);
        var applied = 0;
        var deferred = 0;
        foreach (var observation in observations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var instanceId = observation.InstanceId.ToString();
            projections.TryGetValue(instanceId, out var projection);
            var snapshot = projection is null ? null : new SagaStatisticsSnapshot(projection.Id, projection.OrchestrationKey,
                projection.ArtifactId.ToString(), projection.Revision, projection.Status, projection.StartedOnUtc, projection.ReachedStatuses);
            var entry = new SagaStatisticsEvent(instanceId, observation.OrchestrationKey, observation.ArtifactId.ToString(),
                observation.Revision, observation.Status, observation.StartedOnUtc, observation.OccurredOnUtc);
            var result = projector.Project(snapshot, entry);
            if (result.Disposition == SagaStatisticsProjectionDisposition.Gap)
            {
                deferred++;
                continue;
            }

            if (result.Disposition == SagaStatisticsProjectionDisposition.Applied)
            {
                if (projection is null)
                {
                    projection = new SagaStatisticsProjection { Id = instanceId, ArtifactId = observation.ArtifactId,
                        OrchestrationKey = observation.OrchestrationKey, StartedOnUtc = observation.StartedOnUtc };
                    db.SagaStatisticsProjections.Add(projection);
                    projections.Add(instanceId, projection);
                }
                projection.Revision = result.Snapshot.Revision;
                projection.Status = result.Snapshot.Status;
                projection.ReachedStatuses = result.Snapshot.ReachedStatuses;
                projection.LastObservedOnUtc = observation.OccurredOnUtc;
                observation.StatusChanged = result.StatusChanged;
                observation.StatusFirstReached = result.NewlyReachedStatus is not null;
            }
            observation.Processed = true;
            applied++;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new SagaStatisticsBatchResult(observations.Length, applied, deferred);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another replica won this projection batch. Retry the observations as a
            // coherent group instead of issuing one conflicting transaction per row.
            return new SagaStatisticsBatchResult(observations.Length, 0, observations.Length);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }
}
