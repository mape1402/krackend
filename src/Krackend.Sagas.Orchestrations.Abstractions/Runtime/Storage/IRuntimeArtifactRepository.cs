using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists runtime orchestration artifacts materialized for an environment.
/// </summary>
public interface IRuntimeArtifactRepository
{
    Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default);

    Task DeactivateActiveArtifacts(
        string environmentKey,
        string orchestrationDefinitionKey,
        Id exceptArtifactId,
        CancellationToken cancellationToken = default);

    Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default);

    Task<RuntimeOrchestrationArtifact> GetByVersion(
        string environmentKey,
        string orchestrationDefinitionKey,
        SemanticVersion version,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(
        string environmentKey,
        CancellationToken cancellationToken = default);

    Task<RuntimeOrchestrationArtifact> GetActive(
        string environmentKey,
        string orchestrationDefinitionKey,
        CancellationToken cancellationToken = default);
}
