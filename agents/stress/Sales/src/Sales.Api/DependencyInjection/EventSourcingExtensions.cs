using System.Diagnostics.CodeAnalysis;
using Sales.Persistence;

namespace Microsoft.Extensions.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    internal static class EventSourcingExtensions
    {
        internal static IServiceCollection AddEventSourcingDefaults(this IServiceCollection services)
        {
            services.AddKrackendEntityFrameworkEventStore<AppDbContext>();

            return services;
        }
    }
}
