namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;

using Krackend.Sagas.Orchestrations.Runtime.Ingress;

internal sealed class RecordingIngressConnector : IIngressConector
{
    public List<string> ConnectedConnectorIds { get; } = [];

    public List<string> DisconnectedConnectorIds { get; } = [];

    public Task ConnectAsync(IngressConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ConnectedConnectorIds.Add(configuration.Id);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default)
    {
        DisconnectedConnectorIds.Add(connectorId);
        return Task.CompletedTask;
    }
}
