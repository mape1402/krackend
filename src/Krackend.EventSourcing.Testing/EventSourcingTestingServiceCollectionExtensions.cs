using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Provides dependency injection helpers for event sourcing tests.
/// </summary>
public static class EventSourcingTestingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory event sourcing test store and assertion services.
    /// </summary>
    public static IServiceCollection AddKrackendEventSourcingTesting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IEventSourcingTestEventStore, InMemoryEventSourcingTestEventStore>();
        services.TryAddSingleton<IEventSourcingTestAssertions, EventSourcingTestAssertions>();

        return services;
    }

    /// <summary>
    /// Registers event sourcing testing services in an adapter-friendly shape for external test hosts.
    /// </summary>
    public static IServiceCollection AddKrackendEventSourcingTestingAdapter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKrackendEventSourcingTesting();
        services.TryAddSingleton<IEventSourcingTestingAdapter, EventSourcingTestingAdapter>();

        return services;
    }
}
