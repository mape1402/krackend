using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Metadata;

/// <summary>
/// Provides dependency injection helpers for event execution context.
/// </summary>
public static class EventExecutionContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers a scoped static execution context.
    /// </summary>
    public static IServiceCollection AddEventExecutionContext(
        this IServiceCollection services,
        Func<IServiceProvider, IEventExecutionContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contextFactory);

        services.AddScoped(contextFactory);
        return services;
    }
}
