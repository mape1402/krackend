using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Resolves the runtime artifact that should execute for a trigger.
/// </summary>
public interface IArtifactResolver
{
    /// <summary>
    /// Resolves the active runtime artifact for an environment and orchestration definition.
    /// </summary>
    /// <param name="environmentKey">Runtime environment key.</param>
    /// <param name="orchestrationDefinitionKey">Orchestration definition key.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Active runtime artifact.</returns>
    Task<RuntimeOrchestrationArtifact> ResolveActive(string environmentKey, string orchestrationDefinitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the requested runtime artifact version, or the active artifact when no version is requested.
    /// </summary>
    /// <param name="environmentKey">Runtime environment key.</param>
    /// <param name="orchestrationDefinitionKey">Orchestration definition key.</param>
    /// <param name="artifactVersion">Optional artifact version.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Resolved runtime artifact.</returns>
    Task<RuntimeOrchestrationArtifact> Resolve(
        string environmentKey,
        string orchestrationDefinitionKey,
        string artifactVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a runtime artifact by its persisted identifier.
    /// </summary>
    /// <param name="artifactId">Runtime artifact id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Resolved runtime artifact.</returns>
    Task<RuntimeOrchestrationArtifact> ResolveById(
        Krackend.Sagas.Orchestrations.Abstractions.Primitives.Id artifactId,
        CancellationToken cancellationToken = default);
}
