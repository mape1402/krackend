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

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>Reads a projected rollout before it becomes eligible for message routing.</summary>
    public interface IGetStagedIngressConfigurationAccessor
    {
        /// <summary>Gets projected configurations for a specific pending generation.</summary>
        Task<IReadOnlyCollection<IngressConfiguration>> GetStagedConfigurationAsync(
            string artifactId, long ingressGeneration, CancellationToken cancellationToken = default);
    }

    /// <summary>Reads the selected inactive triggers and active backchannel of a rollback target.</summary>
    public interface IGetRollbackIngressConfigurationAccessor
    {
        /// <summary>Gets projected configurations for a specific ready target generation.</summary>
        Task<IReadOnlyCollection<IngressConfiguration>> GetRollbackConfigurationAsync(
            string artifactId, long ingressGeneration, IReadOnlyCollection<string> triggerKeys, CancellationToken cancellationToken = default);
    }

    /// <summary>Reads only the backchannel of an archived artifact for recovery preparation.</summary>
    public interface IGetArchivedBackchannelConfigurationAccessor
    {
        /// <summary>Gets backchannels for the expected archived generation, including retired backchannels.</summary>
        Task<IReadOnlyCollection<IngressConfiguration>> GetArchivedBackchannelsAsync(
            string artifactId, long ingressGeneration, CancellationToken cancellationToken = default);
    }
}
