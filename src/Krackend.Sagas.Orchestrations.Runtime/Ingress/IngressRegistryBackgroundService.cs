using Microsoft.Extensions.Hosting;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public class IngressRegistryBackgroundService : IHostedService
    {
        private readonly IIngressRegistry _ingressRegistry;

        public IngressRegistryBackgroundService(IIngressRegistry ingressRegistry)
        {
            _ingressRegistry = ingressRegistry ?? throw new ArgumentNullException(nameof(ingressRegistry));
        }

        public Task StartAsync(CancellationToken cancellationToken)
            => _ingressRegistry.StandUpAllAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken)
            => _ingressRegistry.ShutDownAllAsync(cancellationToken);
    }
}
