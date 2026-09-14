using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Projects runtime ingress configuration rows from orchestration artifacts.
    /// </summary>
    public interface IRuntimeIngressConfigurationProjector
    {
        /// <summary>
        /// Projects ingress configurations for the supplied runtime artifact.
        /// </summary>
        /// <param name="artifact">Artifact containing trigger and backchannel configuration.</param>
        /// <param name="cancellationToken">Token used to cancel projection.</param>
        /// <returns>A task that completes when projection has finished.</returns>
        Task ProjectAsync(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default);
    }
}
