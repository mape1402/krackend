using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Stores projected ingress configurations used by runtime replicas.
    /// </summary>
    public interface IRuntimeIngressConfigurationRepository
    {
        /// <summary>
        /// Inserts or updates the ingress configurations for an artifact.
        /// </summary>
        /// <param name="runtimeOrchestrationArtifactId">Runtime artifact identifier.</param>
        /// <param name="configurations">Configurations projected from the artifact.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that completes when the configurations have been stored.</returns>
        Task UpsertForArtifactAsync(
            Id runtimeOrchestrationArtifactId,
            IReadOnlyCollection<RuntimeIngressConfiguration> configurations,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deactivates ingress configurations that belong to the supplied artifacts.
        /// </summary>
        /// <param name="runtimeOrchestrationArtifactIds">Artifact identifiers whose ingress configurations should be deactivated.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that completes when the configurations have been deactivated.</returns>
        Task DeactivateForArtifactsAsync(
            IReadOnlyCollection<Id> runtimeOrchestrationArtifactIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads active ingress configurations using paging.
        /// </summary>
        /// <param name="skip">Number of configurations to skip.</param>
        /// <param name="take">Maximum number of configurations to return.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The active ingress configurations.</returns>
        Task<IReadOnlyCollection<RuntimeIngressConfiguration>> ReadActiveAsync(
            int skip,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets active ingress configurations for a runtime artifact.
        /// </summary>
        /// <param name="runtimeOrchestrationArtifactId">Runtime artifact identifier.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The active ingress configurations for the artifact.</returns>
        Task<IReadOnlyCollection<RuntimeIngressConfiguration>> GetActiveByArtifactIdAsync(
            Id runtimeOrchestrationArtifactId,
            CancellationToken cancellationToken = default);
    }
}
