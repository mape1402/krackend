namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.HotState;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

internal sealed class EntityFrameworkRuntimeHotStateProjectionSink(RuntimeDbContext dbContext)
    : IRuntimeHotStateProjectionSink
{
    public async Task ProjectAsync(
        IReadOnlyCollection<RuntimeHotProjectionBatch> batches,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batches);

        // Only the latest materialized version of an entity matters to the durable
        // projection. Collapsing intermediate transitions is what keeps the journal
        // write-ahead without turning one saga transition into N SQL operations.
        var latest = new Dictionary<string, RuntimeHotStateMutation>(StringComparer.Ordinal);
        foreach (var batch in batches.OrderBy(item => item.Sequence))
        foreach (var mutation in batch.Mutations)
            latest[$"{mutation.EntityType}|{mutation.EntityId}"] = mutation;

        var byType = latest.Values.GroupBy(item => item.EntityType)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        await Apply<OrchestrationInstance, Id>(byType, item => item.Id, cancellationToken);
        await Apply<StageExecution, Id>(byType, item => item.Id, cancellationToken);
        await Apply<TaskExecution, Id>(byType, item => item.Id, cancellationToken);
        await Apply<TaskExecutionAttempt, Id>(byType, item => item.Id, cancellationToken);
        await Apply<TaskDispatch, Id>(byType, item => item.Id, cancellationToken);
        await Apply<ExecutionTransition, Id>(byType, item => item.Id, cancellationToken);
        await Apply<InstanceVariable, Id>(byType, item => item.Id, cancellationToken);
        await Apply<CompensationExecution, Id>(byType, item => item.Id, cancellationToken);
        await Apply<SagaStatisticsObservation, string>(byType, item => item.Id, cancellationToken);

        if (dbContext.ChangeTracker.HasChanges())
            await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task Apply<TEntity, TKey>(
        IReadOnlyDictionary<string, RuntimeHotStateMutation[]> byType,
        Func<TEntity, TKey> keySelector,
        CancellationToken cancellationToken)
        where TEntity : class
        where TKey : notnull
    {
        if (!byType.TryGetValue(typeof(TEntity).FullName!, out var mutations) || mutations.Length == 0)
            return;

        var materialized = mutations.Select(mutation =>
            {
                var entity = (TEntity)RuntimeHotStateEntityCodec.Deserialize(mutation);
                return new Projected<TEntity, TKey>(mutation, entity, keySelector(entity));
            })
            .ToArray();
        var existing = new Dictionary<TKey, TEntity>();
        foreach (var keyBatch in materialized.Select(item => item.Key).Distinct().Chunk(500))
        {
            var keys = keyBatch.ToArray();
            var rows = await dbContext.Set<TEntity>().Where(entity =>
                    keys.Contains(EF.Property<TKey>(entity, "Id")))
                .ToArrayAsync(cancellationToken);
            foreach (var row in rows) existing[keySelector(row)] = row;
        }

        foreach (var item in materialized)
        {
            if (item.Mutation.Operation == RuntimeHotStateOperation.Delete)
            {
                if (existing.TryGetValue(item.Key, out var current)) dbContext.Remove(current);
                continue;
            }
            if (existing.TryGetValue(item.Key, out var durable))
                dbContext.Entry(durable).CurrentValues.SetValues(item.Entity);
            else
                dbContext.Add(item.Entity);
        }
    }

    private sealed record Projected<TEntity, TKey>(
        RuntimeHotStateMutation Mutation,
        TEntity Entity,
        TKey Key)
        where TEntity : class;
}
