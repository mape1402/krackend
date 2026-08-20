namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public interface IIngressConector
    {
        Task ConnectAsync(IngressConfiguration configuration, CancellationToken cancellationToken = default);

        Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default);
    }
}
