using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Runtime.DependencyInjection
{
    /// <summary>
    /// Provides chainable access to runtime service registrations.
    /// </summary>
    public sealed class KrackendOrchestrationsRuntimeBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KrackendOrchestrationsRuntimeBuilder"/> class.
        /// </summary>
        /// <param name="services">Service collection being configured.</param>
        public KrackendOrchestrationsRuntimeBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Gets the service collection being configured.
        /// </summary>
        public IServiceCollection Services { get; }
    }
}
