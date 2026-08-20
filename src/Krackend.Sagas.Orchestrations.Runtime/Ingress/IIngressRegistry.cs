namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public interface IIngressRegistry
    {
        Task StandUpAllAsync(CancellationToken cancellationToken = default);

        Task StandUpOneAsync(string artifactId, CancellationToken cancellationToken = default);

        Task ShutDownAllAsync(CancellationToken cancellationToken = default);

        Task ShutDownOneAsync(string artifactId, CancellationToken cancellationToken = default);
    }
}
