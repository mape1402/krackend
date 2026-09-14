using System.Diagnostics.CodeAnalysis;
using Sales.Business;

namespace Microsoft.Extensions.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    internal static class PipelineExtensions
    {
        internal static IServiceCollection AddPipelineDefaults(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTurtlePathSpiderTransactions(
                configuration,
                typeof(Constants).Assembly,
                typeof(PipelineExtensions).Assembly);

            return services;
        }
    }
}
