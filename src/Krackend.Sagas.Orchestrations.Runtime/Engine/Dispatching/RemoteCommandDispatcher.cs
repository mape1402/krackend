using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching
{
    internal class RemoteCommandDispatcher : IRemoteCommandDispatcher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RemoteCommandDispatcher> _logger;
        private readonly IOrchestrationMessageMetadataSetter _messageMetadataSetter;
        private readonly IOrchestrationPropagationMetadataSetter _propagationMetadataSetter;

        public RemoteCommandDispatcher(
            IServiceProvider serviceProvider,
            ILogger<RemoteCommandDispatcher> logger,
            IOrchestrationMessageMetadataSetter messageMetadataSetter,
            IOrchestrationPropagationMetadataSetter propagationMetadataSetter = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messageMetadataSetter = messageMetadataSetter ?? throw new ArgumentNullException(nameof(messageMetadataSetter));
            _propagationMetadataSetter = propagationMetadataSetter;
        }

        public async Task DispatchAsync(RemoteCommand command, CancellationToken cancellationToken = default)
        {
            var executor = _serviceProvider.GetKeyedService<IRemoteCommandExecutor>(command.RemoteCommandTransport);

            if(executor == null)
            {
                var message = $"No remote command executor is configured for '{command.RemoteCommandTransport}'.";
                _logger.LogError(message);
                throw new RemoteCommandConfigurationException(message);
            }

            _messageMetadataSetter.Set(command.MessageMetadata ?? new OrchestrationMessageMetadata());
            _propagationMetadataSetter?.Set(command.PropagationMetadata ?? new OrchestrationPropagationMetadata());
            await executor.ExecuteAsync(command, cancellationToken);
        }
    }
}
