using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Http
{
    internal sealed class DefaultHttpIngressConnector : IIngressConector
    {
        private readonly ILogger<DefaultHttpIngressConnector> _logger;

        public DefaultHttpIngressConnector(ILogger<DefaultHttpIngressConnector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task ConnectAsync(string settingsPayload, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("HTTP ingress was ignored because no HTTP connector implementation is registered.");
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
