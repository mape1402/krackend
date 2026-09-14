namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging
{
    internal class MessagingRemoteCommandExecutor : IRemoteCommandExecutor
    {
        private readonly IMessagingDispatchAdapter _adapter;
        private readonly IMessagingCommandSerializer _serializer;

        public MessagingRemoteCommandExecutor(IMessagingDispatchAdapter adapter, IMessagingCommandSerializer serializer)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public async Task ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken = default)
        {
            var messagingCommand = _serializer.Deserialize(command.SettingsPayload);
            messagingCommand.Payload = command.Payload;

            await _adapter.PublishAsync(messagingCommand, cancellationToken);
        }
    }
}
