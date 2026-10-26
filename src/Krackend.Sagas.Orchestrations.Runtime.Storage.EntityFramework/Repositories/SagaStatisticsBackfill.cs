using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class SagaStatisticsBackfill(RuntimeDbContext db, IOrchestrationInstanceRepository instances)
    : ISagaStatisticsBackfill
{
    private const string CheckpointId = "legacy-instances-v1";

    public async Task<bool> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (await db.SagaStatisticsBackfillCheckpoints.AsNoTracking().AnyAsync(x => x.Id == CheckpointId, cancellationToken))
            return true;
        var candidates = await db.OrchestrationInstances.AsNoTracking().Where(x => x.StatisticsRevision == 0)
            .OrderBy(x => x.Id).Take(batchSize).ToArrayAsync(cancellationToken);
        foreach (var instance in candidates)
        {
            try { await instances.Update(instance, cancellationToken); }
            catch (DbUpdateConcurrencyException) { }
            finally { db.ChangeTracker.Clear(); }
        }
        if (candidates.Length != 0) return false;
        db.SagaStatisticsBackfillCheckpoints.Add(new SagaStatisticsBackfillCheckpoint
        {
            Id = CheckpointId,
            CompletedOnUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return true;
    }
}
