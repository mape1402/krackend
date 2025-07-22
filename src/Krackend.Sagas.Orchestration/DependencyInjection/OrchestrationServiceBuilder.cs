namespace Microsoft.Extensions.DependencyInjection
{
    internal sealed class OrchestrationServiceBuilder : IOrchestrationServiceBuilder
    {
        public OrchestrationServiceBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IServiceCollection Services { get; }
    }
}
