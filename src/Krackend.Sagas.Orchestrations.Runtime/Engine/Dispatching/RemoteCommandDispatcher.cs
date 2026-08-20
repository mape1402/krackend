using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching
{
    internal class RemoteCommandDispatcher : IRemoteCommandDispatcher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RemoteCommandDispatcher> _logger;

        public RemoteCommandDispatcher(IServiceProvider serviceProvider, ILogger<RemoteCommandDispatcher> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task DispatchAsync(RemoteCommand command, CancellationToken cancellationToken = default)
        {
            var executor = _serviceProvider.GetKeyedService<IRemoteCommandExecutor>(command.RemoteCommandTransport);

            if(executor == null)
            {
                _logger.LogWarning("No has executor configured for '{transport}' transport.", command.RemoteCommandTransport);
                //TODO: Should save log into db for tracking.
                return;
            }

            await executor.ExecuteAsync(command, cancellationToken);
        }
    }
}
