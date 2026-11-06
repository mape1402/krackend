using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>Prepares callback routing before an archived instance resumes execution.</summary>
internal sealed class ArchivedRecoveryIngressPreparer(
    IRuntimeArtifactRepository artifacts,
    IRuntimeStorageUnitOfWork unitOfWork,
    IIngressRegistry ingress,
    IRuntimeArtifactReadyNotifier notifier,
    ILogger<ArchivedRecoveryIngressPreparer> logger)
{
    public async Task PrepareAsync(Id artifactId, CancellationToken cancellationToken)
    {
        var artifact = await artifacts.GetById(artifactId, cancellationToken);
        // The instance coordinator holds its lease while recovery executes. Fence
        // the artifact before inspecting lifecycle state so an archive either
        // observes that lease or completes before this recovery continues.
        if (artifacts is IRuntimeArtifactLifecycleFenceRepository fence)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            using (unitOfWork.DeferAutoSave())
            {
                if (!await fence.TryFenceLifecycle(artifactId, artifact.IngressGeneration, cancellationToken))
                    throw new InvalidOperationException("The orchestration version changed while recovery was being prepared.");
                await unitOfWork.SaveChanges(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            artifact = await artifacts.GetById(artifactId, cancellationToken);
        }
        if (artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Archived &&
            artifact.IsActive && artifact.Status == RuntimeOrchestrationArtifactStatus.Ready) return;
        var alreadyActive = artifact.LifecycleStatus == RuntimeOrchestrationLifecycleStatus.Archived &&
            artifact.IsActive && artifact.Status == RuntimeOrchestrationArtifactStatus.Ready;
        if (!alreadyActive && (artifact.IsActive || artifact.Status != RuntimeOrchestrationArtifactStatus.Retired ||
            artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Archived))
            throw new InvalidOperationException("The orchestration version is not available for recovery.");
        if (artifacts is not IArchivedRecoveryActivationRepository activation || ingress is not IArchivedRecoveryIngressRegistry archivedIngress)
            throw new InvalidOperationException("The runtime storage or ingress does not support recovery of archived orchestration versions.");

        await using var preparation = await archivedIngress.PrepareArchivedRecoveryAsync(artifactId.ToString(), artifact.IngressGeneration, cancellationToken);
        if (!alreadyActive)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            using (unitOfWork.DeferAutoSave())
            {
                var generation = await activation.TryActivateArchivedBackchannelsAsync(artifactId, artifact.IngressGeneration, cancellationToken);
                if (!generation.HasValue)
                    throw new InvalidOperationException("The archived orchestration version changed while recovery was being prepared.");
                await unitOfWork.SaveChanges(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        preparation.Complete();

        // The local connector is ready already. Other replicas can also rediscover
        // the active backchannel from storage during startup if gossip is unavailable.
        try
        {
            artifact = await artifacts.GetById(artifactId, cancellationToken);
            await notifier.NotifyReadyAsync(RuntimeArtifactReadyGossipMessage.FromArtifact(artifact), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not notify other replicas about archived recovery for artifact {ArtifactId}.", artifactId);
        }
    }
}
