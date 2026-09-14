using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using global::Pigeon.Messaging.Producing;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon
{
    internal class PigeonDispatchAdapter : IMessagingDispatchAdapter
    {
        private readonly IProducer _producer;

        public PigeonDispatchAdapter(IProducer producer)
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        public async Task PublishAsync(MessagingCommand command, CancellationToken cancellationToken = default)
        {
            var payload = string.IsNullOrWhiteSpace(command.Payload)
                ? null
                : JsonNode.Parse(command.Payload);

            await _producer.PublishAsync(payload, command.Topic, command.Version, cancellationToken);
        }
    }
}
