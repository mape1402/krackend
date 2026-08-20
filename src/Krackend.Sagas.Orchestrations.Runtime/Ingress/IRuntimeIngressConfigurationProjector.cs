using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public interface IRuntimeIngressConfigurationProjector
    {
        Task ProjectAsync(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default);
    }
}
