namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Reads ingress configurations for a specific ready runtime artifact.
    /// </summary>
    public interface IGetIngressConfigurationByArtifactAccessor
    {
        /// <summary>
        /// Gets the active ingress configurations for the artifact.
        /// </summary>
        /// <param name="artifactId">Runtime artifact id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The artifact ingress configurations.</returns>
        Task<IReadOnlyCollection<IngressConfiguration>> GetConfigurationAsync(string artifactId, CancellationToken cancellationToken = default);
    }
}
