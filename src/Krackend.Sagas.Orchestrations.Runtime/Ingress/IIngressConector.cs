namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public interface IIngressConector
    {
        Task ConnectAsync(string settingsPayload, CancellationToken cancellationToken = default);

        Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default);
    }
}
