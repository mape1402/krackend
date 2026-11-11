using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class SagaFailureScopeBackfill(RuntimeDbContext db) : ISagaFailureScopeBackfill
{
    internal const string CheckpointId = "failure-scope-v1";

    public async Task<bool> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        try { return await ProcessBatchAsync(batchSize, cancellationToken); }
        catch { db.ChangeTracker.Clear(); throw; }
    }

    private async Task<bool> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var checkpoint = await db.SagaFailureBackfillCheckpoints.SingleOrDefaultAsync(x => x.Id == CheckpointId, cancellationToken);
        if (checkpoint is null)
        {
            checkpoint = new SagaFailureBackfillCheckpoint { Id = CheckpointId, BeforeUtc = DateTime.UtcNow,
                CursorUtc = DateTime.MinValue, ConcurrencyToken = Guid.NewGuid().ToString("N") };
            db.SagaFailureBackfillCheckpoints.Add(checkpoint);
            await db.SaveChangesAsync(cancellationToken);
        }
        // A null version also matches documents created by older Mongo hosts.
        var page = await db.SagaFailureEvents.Where(x => x.ScopeProjectionVersion == null)
            .OrderBy(x => x.OccurredOnUtc).ThenBy(x => x.Id).Take(batchSize).ToArrayAsync(cancellationToken);
        if (page.Length == 0)
        {
            if (!checkpoint.Completed)
            {
                checkpoint.Completed = true;
                checkpoint.ConcurrencyToken = Guid.NewGuid().ToString("N");
                await db.SaveChangesAsync(cancellationToken);
            }
            return true;
        }
        var instances = new Dictionary<Id, (string Key, string Version)>();
        foreach (var chunk in page.Select(x => x.OrchestrationInstanceId).Distinct().Chunk(200))
        {
            var matches = await db.OrchestrationInstances.AsNoTracking().Where(x => chunk.Contains(x.Id))
                .Select(x => new { x.Id, x.OrchestrationDefinitionKey, x.OrchestrationVersion }).ToArrayAsync(cancellationToken);
            foreach (var item in matches) instances[item.Id] = (item.OrchestrationDefinitionKey, item.OrchestrationVersion);
        }
        foreach (var item in page)
        {
            if (instances.TryGetValue(item.OrchestrationInstanceId, out var scope))
            {
                item.OrchestrationKey = string.IsNullOrWhiteSpace(scope.Key) ? null : scope.Key.Trim();
                item.OrchestrationVersion = string.IsNullOrWhiteSpace(scope.Version) ? null : scope.Version.Trim();
            }
            // Missing instances and missing versions are reported as unclassified.
            item.ScopeProjectionVersion = 1;
        }
        checkpoint.Completed = false;
        checkpoint.ConcurrencyToken = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(cancellationToken);
        return false;
    }
}
