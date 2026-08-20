namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    internal class MessagingIngressConnector : IIngressConector
    {
        private readonly IMessagingAdapter _messagingAdapter;
        private readonly IMessagingConfigurationSerializer _serializer;

        public MessagingIngressConnector(IMessagingAdapter messagingAdapter, IMessagingConfigurationSerializer serializer)
        {
            _messagingAdapter = messagingAdapter ?? throw new ArgumentNullException(nameof(messagingAdapter));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public async Task ConnectAsync(string settingsPayload, CancellationToken cancellationToken = default)
        {
            var configuration = _serializer.Deserialize(settingsPayload);
            await _messagingAdapter.ConnectAsync(configuration, cancellationToken);
        }

        public Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask; // TODO: implement Disconnect!
        }
    }
}
