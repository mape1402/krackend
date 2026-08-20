using Microsoft.Extensions.Logging;

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
            _logger.LogWarning(
                "Messaging command for topic '{topic}' and version '{version}' was ignored because no messaging dispatch adapter is registered.",
                command.Topic,
                command.Version);

            return Task.CompletedTask;
        }
    }
}
