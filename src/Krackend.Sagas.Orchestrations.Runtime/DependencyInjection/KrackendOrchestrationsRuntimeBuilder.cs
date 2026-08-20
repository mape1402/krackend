using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Runtime.DependencyInjection
{
    public sealed class KrackendOrchestrationsRuntimeBuilder
    {
        public KrackendOrchestrationsRuntimeBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IServiceCollection Services { get; }
    }
}
