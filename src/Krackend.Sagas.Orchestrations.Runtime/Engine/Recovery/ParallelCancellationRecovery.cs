using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

internal static class ParallelCancellationRecovery
{
    internal const string ResumeMetadataKey = "Recovery.ResumeCancelled";
    internal const string ResumeFailedMetadataKey = "Recovery.ResumeFailed";

    internal static async Task<OrchestrationRecoveryResult> PrepareAsync(
        OrchestrationInstance instance, IReadOnlyCollection<TaskExecution> cancelled,
        ITaskExecutionRepository tasks, ITaskExecutionAttemptRepository attempts,
        ITaskDispatchRepository dispatches, IRuntimeStorageUnitOfWork unitOfWork,
        IOrchestrationDispatchCancellationCoordinator cancellations, CancellationToken cancellationToken)
    {
        foreach (var task in cancelled)
        {
            var taskAttempts = await attempts.GetByTaskExecutionId(task.Id, cancellationToken);
            var attempt = taskAttempts.OrderByDescending(x => x.AttemptNumber).FirstOrDefault();
            var dispatch = attempt?.DispatchId is { } id ? await dispatches.TryGetById(id, cancellationToken) : null;
            if (dispatch?.PhysicalCancellationPending == true && cancellations is IOrchestrationDispatchCancellationRecoveryCoordinator recovery)
            {
                await recovery.ReconcileAsync(dispatch.Id, cancellationToken);
                dispatch = await dispatches.TryGetById(dispatch.Id, cancellationToken);
            }
            var providerStatus = dispatch?.Metadata is not null &&
                dispatch.Metadata.TryGetValue("Recovery.DispatchCancellationProviderStatus", out var value) ? value?.ToString() : null;
            if (attempt is null || attempt.TaskExecutionId != task.Id || attempt.AttemptNumber != task.LastAttemptNumber ||
                attempt.Status != TaskExecutionStatus.Cancelled || dispatch is null || dispatch.TaskExecutionAttemptId != attempt.Id ||
                !string.Equals(dispatch.DispatchStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                dispatch.SentOnUtc is not null || dispatch.PhysicalCancellationPending || dispatch.PhysicalCancellationCompletedOnUtc is null ||
                !(string.Equals(providerStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(providerStatus, "AlreadyCancelled", StringComparison.OrdinalIgnoreCase)))
                return OrchestrationRecoveryResult.Rejected(instance.Id.ToString(), instance.Status.ToString(),
                    $"Task '{task.TaskKey}' cannot be safely resumed because its previous delivery or cancellation is uncertain.");
        }

        if (cancelled.Count == 0) return null;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        using (unitOfWork.DeferAutoSave())
        {
            foreach (var task in cancelled)
            {
                task.Metadata[ResumeMetadataKey] = JsonValue.Create(true);
                await tasks.Update(task, cancellationToken);
            }
            await unitOfWork.SaveChanges(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return null;
    }
}
