using Microsoft.Extensions.Hosting;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public class IngressRegistryBackgroundService : BackgroundService
    {
        private readonly IIngressRegistry _ingressRegistry;

        public IngressRegistryBackgroundService(IIngressRegistry ingressRegistry)
        {
            _ingressRegistry = ingressRegistry ?? throw new ArgumentNullException(nameof(ingressRegistry));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) 
            => await _ingressRegistry.StandUpAllAsync(stoppingToken);
    }
}
