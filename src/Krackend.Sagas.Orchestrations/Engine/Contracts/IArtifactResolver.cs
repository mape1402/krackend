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
}
