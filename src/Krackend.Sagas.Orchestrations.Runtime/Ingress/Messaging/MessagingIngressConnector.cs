namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    internal class MessagingIngressConnector : IIngressConector, IRevalidatableIngressConector
    {
        private readonly IMessagingIngressAdapter _messagingAdapter;
        private readonly IMessagingConfigurationSerializer _serializer;

        public MessagingIngressConnector(IMessagingIngressAdapter messagingAdapter, IMessagingConfigurationSerializer serializer)
        {
            _messagingAdapter = messagingAdapter ?? throw new ArgumentNullException(nameof(messagingAdapter));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public async Task ConnectAsync(IngressConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var config = _serializer.Deserialize(configuration.SettingsPayload);
            config.IngressKind = configuration.IngressKind;
            config.ArtifactId = configuration.ArtifactId;
            config.IsStaged = configuration.IsStaged;
            config.IngressTransport = configuration.IngressTransport;
            config.ConnectorId = configuration.Id;

            await _messagingAdapter.ConnectAsync(config, cancellationToken);
        }

        public Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default)
        {
            return _messagingAdapter.DisconnectAsync(connectorId, cancellationToken);
        }

        public Task RevalidateAsync(IngressConfiguration configuration, CancellationToken cancellationToken = default)
            => ConnectAsync(configuration, cancellationToken);
    }
}
