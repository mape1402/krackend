using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists runtime orchestration artifacts materialized for an environment.
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
    /// <param name="environmentKey">Runtime environment key.</param>
    /// <param name="orchestrationDefinitionKey">Orchestration definition key.</param>
    /// <param name="exceptArtifactId">Artifact id that must remain active.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeactivateActiveArtifacts(
        string environmentKey,
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
    /// <param name="environmentKey">Runtime environment key.</param>
    /// <param name="orchestrationDefinitionKey">Orchestration definition key.</param>
    /// <param name="version">Semantic version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime artifact.</returns>
    Task<RuntimeOrchestrationArtifact> GetByVersion(
        string environmentKey,
        string orchestrationDefinitionKey,
        SemanticVersion version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all artifacts for an environment.
    /// </summary>
    /// <param name="environmentKey">Runtime environment key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime artifacts.</returns>
    Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(
        string environmentKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets ready artifacts for an environment.
    /// </summary>
    /// <param name="environmentKey">Runtime environment key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ready runtime artifacts.</returns>
    Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetReady(
        string environmentKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active ready artifact for an orchestration definition.
    /// </summary>
    /// <param name="environmentKey">Runtime environment key.</param>
    /// <param name="orchestrationDefinitionKey">Orchestration definition key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active runtime artifact.</returns>
    Task<RuntimeOrchestrationArtifact> GetActive(
        string environmentKey,
        string orchestrationDefinitionKey,
        CancellationToken cancellationToken = default);
}
