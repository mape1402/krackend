using Microsoft.Extensions.Hosting;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Starts and stops all configured runtime ingress connectors with the host lifecycle.
    /// </summary>
    public class IngressRegistryBackgroundService : IHostedService
    {
        private readonly IIngressRegistry _ingressRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="IngressRegistryBackgroundService"/> class.
        /// </summary>
        /// <param name="ingressRegistry">Ingress registry used to manage connector lifecycle.</param>
        public IngressRegistryBackgroundService(IIngressRegistry ingressRegistry)
        {
            _ingressRegistry = ingressRegistry ?? throw new ArgumentNullException(nameof(ingressRegistry));
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
            => _ingressRegistry.StandUpAllAsync(cancellationToken);

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
            => _ingressRegistry.ShutDownAllAsync(cancellationToken);
    }
}
