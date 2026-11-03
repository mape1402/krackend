using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class SagaStatisticsBackfill(RuntimeDbContext db)
    : ISagaStatisticsBackfill
{
    private const string CheckpointId = "legacy-instances-v1";

    public async Task<bool> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (await db.SagaStatisticsBackfillCheckpoints.AsNoTracking().AnyAsync(x => x.Id == CheckpointId, cancellationToken))
            return true;
        var candidates = await db.OrchestrationInstances.AsNoTracking().Where(x => x.StatisticsRevision == 0)
            .OrderBy(x => x.Id).Take(batchSize)
            .Select(x => new
            {
                x.Id, x.RuntimeOrchestrationArtifactId, x.OrchestrationDefinitionKey, x.CorrelationId,
                x.SagaId, x.ExecutionKey, x.Status, x.CurrentStageKey, x.CurrentTaskKey,
                x.LastExecutedStageKey, x.LastExecutedTaskKey, x.StartedOnUtc, x.LastUpdatedOnUtc,
                x.StatisticsRevision
            }).ToArrayAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            try
            {
                var instance = new OrchestrationInstance
                {
                    Id = candidate.Id, RuntimeOrchestrationArtifactId = candidate.RuntimeOrchestrationArtifactId,
                    OrchestrationDefinitionKey = candidate.OrchestrationDefinitionKey, CorrelationId = candidate.CorrelationId,
                    SagaId = candidate.SagaId, ExecutionKey = candidate.ExecutionKey, Status = candidate.Status,
                    CurrentStageKey = candidate.CurrentStageKey, CurrentTaskKey = candidate.CurrentTaskKey,
                    LastExecutedStageKey = candidate.LastExecutedStageKey, LastExecutedTaskKey = candidate.LastExecutedTaskKey,
                    StartedOnUtc = candidate.StartedOnUtc, LastUpdatedOnUtc = candidate.LastUpdatedOnUtc,
                    StatisticsRevision = candidate.StatisticsRevision
                };
                db.OrchestrationInstances.Attach(instance);
                instance.CaptureLastExecutionCursor();
                instance.StatisticsRevision = 1;
                db.SagaStatisticsObservations.Add(new SagaStatisticsObservation
                {
                    Id = $"{instance.Id}:{instance.StatisticsRevision:D20}",
                    InstanceId = instance.Id, ArtifactId = instance.RuntimeOrchestrationArtifactId,
                    OrchestrationKey = instance.OrchestrationDefinitionKey, Revision = instance.StatisticsRevision,
                    Status = instance.Status, StageKey = instance.CurrentStageKey, StartedOnUtc = instance.StartedOnUtc,
                    OccurredOnUtc = instance.LastUpdatedOnUtc, IsBackfill = true
                });
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException) { }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A competing writer may create revision 1 before this save. Some
                // providers report the duplicate observation before the stale update.
                // Suppress the failure only if both committed records prove that the
                // candidate was already processed; otherwise preserve the error.
                db.ChangeTracker.Clear();
                var advanced = await db.OrchestrationInstances.AsNoTracking()
                    .AnyAsync(x => x.Id == candidate.Id && x.StatisticsRevision > 0, cancellationToken);
                var observationId = $"{candidate.Id}:{1L:D20}";
                if (!advanced || !await db.SagaStatisticsObservations.AsNoTracking()
                    .AnyAsync(x => x.Id == observationId && x.InstanceId == candidate.Id && x.Revision == 1, cancellationToken))
                    throw;
            }
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
