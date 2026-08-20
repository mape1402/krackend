using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    internal sealed class DefaultMessagingAdapter : IMessagingIngressAdapter
    {
        private readonly ILogger<DefaultMessagingAdapter> _logger;

        public DefaultMessagingAdapter(ILogger<DefaultMessagingAdapter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task ConnectAsync(MessagingConfiguration configuration, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning(
                "Messaging ingress for artifact '{artifactId}' was ignored because no messaging adapter is registered.",
                configuration?.ArtifactId);

            return Task.CompletedTask;
        }
    }
}
