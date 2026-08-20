namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public interface IGetIngressConfigurationByArtifactAccessor
    {
        Task<IReadOnlyCollection<IngressConfiguration>> GetConfigurationAsync(string artifactId, CancellationToken cancellationToken = default);
    }
}
