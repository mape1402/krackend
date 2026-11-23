using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class RuntimeHistoryRetentionProcessor(RuntimeDbContext db)
    : IRuntimeHistoryRetentionProcessor
{
    public async Task<RuntimeHistoryRetentionResult> ProcessAsync(
        DateTime completedBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 5_000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        completedBeforeUtc = DateTime.SpecifyKind(completedBeforeUtc, DateTimeKind.Utc);

        // Completed and compensated are immutable terminal outcomes. Recoverable and
        // operator-significant outcomes intentionally remain available indefinitely.
        var instanceIds = await db.OrchestrationInstances.AsNoTracking()
            .Where(instance =>
                (instance.Status == OrchestrationInstanceStatus.Completed ||
                 instance.Status == OrchestrationInstanceStatus.Compensated) &&
                instance.LastUpdatedOnUtc < completedBeforeUtc &&
                !instance.DispatchCancellationPending &&
                (instance.ActiveLeaseId == null || instance.ActiveLeaseId == string.Empty))
            .OrderBy(instance => instance.StartedOnUtc)
            .ThenBy(instance => instance.Id)
            .Select(instance => instance.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        if (instanceIds.Length == 0) return new RuntimeHistoryRetentionResult(0, 0, 0);

        // A retained execution is a graph, not a collection of independently
        // disposable rows. Keep the child cleanup and final parent fence atomic so
        // a shutdown, deadlock or competing sweeper can never leave a partial graph.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var taskIds = await db.TaskExecutions.AsNoTracking()
            .Where(task => instanceIds.Contains(task.OrchestrationInstanceId))
            .Select(task => task.Id)
            .ToArrayAsync(cancellationToken);
        var attemptIds = taskIds.Length == 0
            ? Array.Empty<Id>()
            : await db.TaskExecutionAttempts.AsNoTracking()
                .Where(attempt => taskIds.Contains(attempt.TaskExecutionId))
                .Select(attempt => attempt.Id)
                .ToArrayAsync(cancellationToken);

        var removedRows = 0;
        if (attemptIds.Length > 0)
        {
            removedRows += await db.TaskDispatches
                .Where(dispatch => attemptIds.Contains(dispatch.TaskExecutionAttemptId))
                .ExecuteDeleteAsync(cancellationToken);
        }
        if (taskIds.Length > 0)
        {
            removedRows += await db.TaskExecutionAttempts
                .Where(attempt => taskIds.Contains(attempt.TaskExecutionId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        removedRows += await db.CompensationExecutions
            .Where(item => instanceIds.Contains(item.OrchestrationInstanceId))
            .ExecuteDeleteAsync(cancellationToken);
        removedRows += await db.ExecutionTransitions
            .Where(item => instanceIds.Contains(item.OrchestrationInstanceId))
            .ExecuteDeleteAsync(cancellationToken);
        removedRows += await db.InstanceVariables
            .Where(item => instanceIds.Contains(item.OrchestrationInstanceId))
            .ExecuteDeleteAsync(cancellationToken);
        removedRows += await db.SagaFailureEvents
            .Where(item => instanceIds.Contains(item.OrchestrationInstanceId))
            .ExecuteDeleteAsync(cancellationToken);
        removedRows += await db.SagaStatisticsObservations
            .Where(item => instanceIds.Contains(item.InstanceId))
            .ExecuteDeleteAsync(cancellationToken);

        var projectionIds = instanceIds.Select(id => id.ToString()).ToArray();
        removedRows += await db.SagaStatisticsProjections
            .Where(item => projectionIds.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);
        if (taskIds.Length > 0)
        {
            removedRows += await db.TaskExecutions
                .Where(task => instanceIds.Contains(task.OrchestrationInstanceId))
                .ExecuteDeleteAsync(cancellationToken);
        }
        removedRows += await db.StageExecutions
            .Where(stage => instanceIds.Contains(stage.OrchestrationInstanceId))
            .ExecuteDeleteAsync(cancellationToken);

        // Reapply every eligibility fence to the final parent delete. Successful
        // terminal states are immutable, but this still prevents an unexpected lease
        // or cancellation fence from being removed after the candidate read.
        var removedInstances = await db.OrchestrationInstances
            .Where(instance => instanceIds.Contains(instance.Id) &&
                (instance.Status == OrchestrationInstanceStatus.Completed ||
                 instance.Status == OrchestrationInstanceStatus.Compensated) &&
                instance.LastUpdatedOnUtc < completedBeforeUtc &&
                !instance.DispatchCancellationPending &&
                (instance.ActiveLeaseId == null || instance.ActiveLeaseId == string.Empty))
            .ExecuteDeleteAsync(cancellationToken);
        if (removedInstances != instanceIds.Length)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new RuntimeHistoryRetentionResult(instanceIds.Length, 0, 0);
        }

        removedRows += removedInstances;
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return new RuntimeHistoryRetentionResult(instanceIds.Length, removedInstances, removedRows);
    }
}
