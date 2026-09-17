namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;

internal sealed class RecordingMessagingIngressAdapter : IMessagingIngressAdapter
{
    private readonly List<MessagingConfiguration> _connected = [];
    private readonly List<string> _disconnected = [];

    public IReadOnlyList<MessagingConfiguration> Connected => _connected;

    public IReadOnlyList<string> Disconnected => _disconnected;

    public Task ConnectAsync(MessagingConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _connected.Add(new MessagingConfiguration
        {
            ConnectorId = configuration.ConnectorId,
            ArtifactId = configuration.ArtifactId,
            Topic = configuration.Topic,
            Version = configuration.Version,
            IngressKind = configuration.IngressKind,
            IngressTransport = configuration.IngressTransport
        });

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default)
    {
        _disconnected.Add(connectorId);
        return Task.CompletedTask;
    }
}
