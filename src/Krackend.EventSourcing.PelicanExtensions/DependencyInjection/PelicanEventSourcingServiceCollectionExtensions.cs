using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.PelicanExtensions.DependencyInjection;

/// <summary>
/// Provides dependency injection extensions for optional Pelican event sourcing integration.
/// </summary>
public static class PelicanEventSourcingServiceCollectionExtensions
{
    /// <summary>
    /// Registers optional Pelican integration services for Krackend event sourcing.
    /// </summary>
    public static IServiceCollection AddKrackendEventSourcingPelicanExtensions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
