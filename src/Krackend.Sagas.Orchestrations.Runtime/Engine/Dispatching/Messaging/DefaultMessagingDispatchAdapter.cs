using Microsoft.Extensions.Logging;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging
{
    internal sealed class DefaultMessagingDispatchAdapter : IMessagingDispatchAdapter
    {
        private readonly ILogger<DefaultMessagingDispatchAdapter> _logger;

        public DefaultMessagingDispatchAdapter(ILogger<DefaultMessagingDispatchAdapter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task PublishAsync(MessagingCommand command, CancellationToken cancellationToken = default)
        {
            var message = $"No messaging dispatch adapter is registered for topic '{command.Topic}' and version '{command.Version}'.";

            _logger.LogError(
                "Messaging command for topic '{topic}' and version '{version}' cannot be published because no messaging dispatch adapter is registered.",
                command.Topic,
                command.Version);

            throw new RemoteCommandConfigurationException(message);
        }
    }
}
