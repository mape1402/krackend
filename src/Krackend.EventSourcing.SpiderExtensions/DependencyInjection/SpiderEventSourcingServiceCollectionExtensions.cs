using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.SpiderExtensions.DependencyInjection;

/// <summary>
/// Provides dependency injection extensions for optional Spider event sourcing integration.
/// </summary>
public static class SpiderEventSourcingServiceCollectionExtensions
{
    /// <summary>
    /// Registers optional Spider integration services for Krackend event sourcing.
    /// </summary>
    public static IServiceCollection AddKrackendEventSourcingSpiderExtensions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
