using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Pigeon.Messaging.Producing;

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
            await _producer.PublishAsync(command.Payload, command.Topic, command.Version, cancellationToken);
        }
    }
}
