using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class SagaFailureStatisticsBackfill(RuntimeDbContext db) : ISagaFailureStatisticsBackfill
{
    internal const string CheckpointId = "failure-transitions-v1";

    public async Task<bool> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        try { return await ProcessBatchAsync(batchSize, cancellationToken); }
        catch
        {
            // A concurrent worker may win the checkpoint or event insert. Do not let
            // failed tracked changes leak into the other statistics work in this scope.
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<bool> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var checkpoint = await db.SagaFailureBackfillCheckpoints.SingleOrDefaultAsync(x => x.Id == CheckpointId, cancellationToken);
        if (checkpoint is null)
        {
            checkpoint = new SagaFailureBackfillCheckpoint
            {
                Id = CheckpointId, BeforeUtc = DateTime.UtcNow, CursorUtc = DateTime.MinValue,
                ConcurrencyToken = Guid.NewGuid().ToString("N")
            };
            db.SagaFailureBackfillCheckpoints.Add(checkpoint);
            await db.SaveChangesAsync(cancellationToken);
        }
        if (checkpoint.Completed) return true;

        // Offset applies only to rows at the last timestamp, not to all prior history.
        // The transition ID makes ordering stable even when many events share a timestamp.
        var page = await db.ExecutionTransitions.AsNoTracking()
            .Where(x => x.OccurredOnUtc >= checkpoint.CursorUtc && x.OccurredOnUtc < checkpoint.BeforeUtc &&
                SagaFailureStatistics.TransitionTypes.Contains(x.TransitionType))
            .OrderBy(x => x.OccurredOnUtc).ThenBy(x => x.Id)
            .Skip(checkpoint.CursorOffset).Take(batchSize)
            .Select(x => new { x.Id, x.OrchestrationInstanceId, x.OccurredOnUtc, x.TransitionType })
            .ToArrayAsync(cancellationToken);

        if (page.Length == 0)
            checkpoint.Completed = true;
        else
        {
            var ids = page.Select(x => x.Id).ToArray();
            var existing = new HashSet<Abstractions.Primitives.Id>();
            foreach (var chunk in ids.Chunk(200))
                foreach (var id in await db.SagaFailureEvents.AsNoTracking().Where(x => chunk.Contains(x.Id))
                    .Select(x => x.Id).ToArrayAsync(cancellationToken)) existing.Add(id);
            foreach (var item in page.Where(x => !existing.Contains(x.Id)))
                db.SagaFailureEvents.Add(new SagaFailureEvent { Id = item.Id, OrchestrationInstanceId = item.OrchestrationInstanceId,
                    OccurredOnUtc = item.OccurredOnUtc, TransitionType = item.TransitionType });
            var last = page[^1].OccurredOnUtc;
            var atLastTimestamp = page.Count(x => x.OccurredOnUtc == last);
            checkpoint.CursorOffset = last == checkpoint.CursorUtc ? checkpoint.CursorOffset + atLastTimestamp : atLastTimestamp;
            checkpoint.CursorUtc = last;
        }
        checkpoint.ConcurrencyToken = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(cancellationToken);
        return checkpoint.Completed;
    }
}
