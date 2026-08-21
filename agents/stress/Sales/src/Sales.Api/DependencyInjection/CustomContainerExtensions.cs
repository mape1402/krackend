using System.Diagnostics.CodeAnalysis;
using Sales.Api.Orchestrations;

namespace Microsoft.Extensions.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    internal static class CustomContainerExtensions
    {
        internal static IServiceCollection AddCustomContainer(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ISaleCreatedOrchestrationDispatcher, SaleCreatedOrchestrationDispatcher>();

            return services;
        }
    }
}
