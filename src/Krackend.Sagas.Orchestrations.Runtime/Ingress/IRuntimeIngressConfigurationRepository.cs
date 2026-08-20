using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public interface IRuntimeIngressConfigurationRepository
    {
        Task UpsertForArtifactAsync(
            Id runtimeOrchestrationArtifactId,
            IReadOnlyCollection<RuntimeIngressConfiguration> configurations,
            CancellationToken cancellationToken = default);

        Task DeactivateForArtifactsAsync(
            IReadOnlyCollection<Id> runtimeOrchestrationArtifactIds,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<RuntimeIngressConfiguration>> ReadActiveAsync(
            int skip,
            int take,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<RuntimeIngressConfiguration>> GetActiveByArtifactIdAsync(
            Id runtimeOrchestrationArtifactId,
            CancellationToken cancellationToken = default);
    }
}
