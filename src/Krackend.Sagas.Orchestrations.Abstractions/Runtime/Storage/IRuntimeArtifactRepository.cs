using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Supports guarded state transitions for a staged rollout.</summary>
public interface IRuntimeArtifactRolloutTransitionRepository
{
    /// <summary>Starts projection only while this rollout generation is still pending or retryable.</summary>
    Task<bool> TryMarkRolloutProjectionStarted(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default);

    /// <summary>Stages a ready transition only for the expected pending or failed generation.</summary>
    Task<bool> TryMarkRolloutReady(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default);

    /// <summary>Conditionally advances a ready target generation before switching rollback triggers.</summary>
    Task<bool> TryFenceRollbackTarget(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default);

    /// <summary>Marks a failed connector only while the generation has not become ready.</summary>
    Task<bool> TryMarkRolloutStandupFailed(Id artifactId, long ingressGeneration, string error, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists runtime orchestration artifacts materialized for a runtime node.
/// </summary>
public interface IRuntimeArtifactRepository
{
    /// <summary>
    /// Creates or updates a runtime orchestration artifact.
    /// </summary>
    /// <param name="artifact">Runtime artifact.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks ingress projection as started for the specified artifact generation.
    /// </summary>
    /// <param name="artifactId">Runtime artifact id.</param>
    /// <param name="ingressGeneration">Ingress generation being projected.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkProjectionStarted(
        Id artifactId,
        long ingressGeneration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks ingress projection as ready for the specified artifact generation.
    /// </summary>
    /// <param name="artifactId">Runtime artifact id.</param>
    /// <param name="ingressGeneration">Ingress generation being completed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkReady(
        Id artifactId,
        long ingressGeneration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks ingress projection as failed for the specified artifact generation.
    /// </summary>
    /// <param name="artifactId">Runtime artifact id.</param>
    /// <param name="ingressGeneration">Ingress generation being projected.</param>
    /// <param name="error">Projection error message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkProjectionFailed(
        Id artifactId,
        long ingressGeneration,
        string error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates active artifacts that were superseded by another artifact.
    /// </summary>
    /// <param name="orchestrationDefinitionKey">Orchestration definition key.</param>
    /// <param name="exceptArtifactId">Artifact id that must remain active.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeactivateActiveArtifacts(
        string orchestrationDefinitionKey,
        Id exceptArtifactId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an artifact by id.
    /// </summary>
    /// <param name="artifactId">Runtime artifact id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime artifact.</returns>
    Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an artifact by orchestration version.
    /// </summary>
    /// <param name="orchestrationDefinitionKey">Orchestration definition key.</param>
    /// <param name="version">Semantic version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime artifact.</returns>
    Task<RuntimeOrchestrationArtifact> GetByVersion(
        string orchestrationDefinitionKey,
        SemanticVersion version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all artifacts for this runtime node.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime artifacts.</returns>
    Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets ready artifacts for this runtime node.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ready runtime artifacts.</returns>
    Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetReady(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active ready artifact for an orchestration definition.
    /// </summary>
    /// <param name="orchestrationDefinitionKey">Orchestration definition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active runtime artifact.</returns>
    Task<RuntimeOrchestrationArtifact> GetActive(
        string orchestrationDefinitionKey,
        CancellationToken cancellationToken = default);
}

/// <summary>Provides bounded artifact payload pages for background lifetime enforcement.</summary>
public interface IRuntimeArtifactLifetimePolicyRepository
{
    /// <summary>Reads active artifact policies in a deterministic, bounded page.</summary>
    Task<IReadOnlyCollection<RuntimeArtifactLifetimePolicyItem>> GetLifetimePolicyPage(
        int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>An artifact identity and its immutable policy payload.</summary>
public sealed record RuntimeArtifactLifetimePolicyItem(Id Id, System.Text.Json.Nodes.JsonNode ArtifactPayload);

/// <summary>Coordinates lifecycle transitions against concurrent instance recovery.</summary>
public interface IRuntimeArtifactLifecycleFenceRepository
{
    /// <summary>
    /// Advances the artifact's existing generation conditionally. The caller must flush it
    /// inside a storage transaction before inspecting instance activity, and keep the transaction open.
    /// </summary>
    Task<bool> TryFenceLifecycle(Id artifactId, long expectedGeneration, CancellationToken cancellationToken = default);
}

/// <summary>Supports bounded cleanup of backchannels temporarily restored for archived instances.</summary>
public interface IArchivedRecoveryRetirementRepository
{
    /// <summary>Reads active archived generations in stable pages.</summary>
    Task<IReadOnlyCollection<ArchivedRecoveryCandidate>> GetArchivedRecoveryCandidates(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Reads inactive archived artifact ids among the supplied local connector owners.</summary>
    Task<IReadOnlyCollection<Id>> GetRetiredArchivedIds(IReadOnlyCollection<Id> artifactIds, CancellationToken cancellationToken = default);

}

/// <summary>
/// Stages lifecycle fences and archived recovery retirements for bounded batches. The caller must
/// flush the fences, recheck instance activity, and commit both operations in the same transaction.
/// </summary>
public interface IArchivedRecoveryBatchRepository
{
    /// <summary>Conditionally advances matching generations and returns their new generations.</summary>
    Task<IReadOnlyCollection<ArchivedRecoveryCandidate>> FenceArchivedRecoveries(IReadOnlyCollection<ArchivedRecoveryCandidate> candidates, CancellationToken cancellationToken = default);

    /// <summary>Stages retirement of fenced archived generations and their backchannels.</summary>
    Task<IReadOnlyCollection<Id>> RetireArchivedRecoveries(IReadOnlyCollection<ArchivedRecoveryCandidate> candidates, CancellationToken cancellationToken = default);
}

/// <summary>An archived version with a temporarily restored callback generation.</summary>
public sealed record ArchivedRecoveryCandidate(Id ArtifactId, long IngressGeneration);
