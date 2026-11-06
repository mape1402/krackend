using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>Completes a rollout after a replica has started its connectors.</summary>
public static class RuntimeArtifactRolloutActivation
{
    /// <summary>Atomically switches the matching triggers and marks the new generation ready.</summary>
    public static async Task ActivateAsync(RuntimeArtifactProjectionRequest request,
        IRuntimeArtifactRepository artifacts, IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeIngressConfigurationRepository ingress, IRuntimeTriggerClaimRepository claims,
        CancellationToken cancellationToken = default, IRuntimeArtifactDeploymentHistoryRepository history = null)
    {
        if (artifacts is not IRuntimeArtifactRolloutTransitionRepository transitions || unitOfWork is null)
            throw new IngressProjectionConfigurationException("The runtime storage cannot safely activate a staged rollout.");
        var artifactId = new Id(Ulid.Parse(request.ArtifactId));
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        using (unitOfWork.DeferAutoSave())
        {
            var rollback = (request.RollbackTargetTriggerKeys?.Length ?? 0) > 0;
            if (rollback)
            {
                if (!await transitions.TryFenceRollbackTarget(artifactId, request.IngressGeneration, cancellationToken))
                    throw new IngressProjectionConfigurationException("The rollback target is no longer the expected ready generation.");
            }
            else if (!await transitions.TryMarkRolloutReady(artifactId, request.IngressGeneration, cancellationToken))
            {
                var current = await artifacts.GetById(artifactId, cancellationToken);
                if (current.IngressGeneration == request.IngressGeneration &&
                    current.Status == RuntimeOrchestrationArtifactStatus.Ready)
                    return;
                throw new IngressProjectionConfigurationException("The staged rollout is no longer the current active generation.");
            }
            await RuntimeArtifactRolloutHandoff.ApplyAsync(request, ingress, claims, cancellationToken);
            if (rollback && (history is not IRuntimeArtifactRollbackStatusRepository rollbackStatus ||
                !await rollbackStatus.TrySetRollbackStatus(request.RollbackOperationId, RuntimeOrchestrationArtifactStatus.Ready, cancellationToken)))
                throw new IngressProjectionConfigurationException("The rollback outcome could not be recorded.");
            await unitOfWork.SaveChanges(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
