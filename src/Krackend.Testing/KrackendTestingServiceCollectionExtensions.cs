using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Testing;

/// <summary>
/// Provides dependency injection helpers for Krackend testing.
/// </summary>
public static class KrackendTestingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory Krackend test event store.
    /// </summary>
    public static IServiceCollection AddKrackendTesting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IKrackendTestEventStore, InMemoryKrackendTestEventStore>();

        return services;
    }

    /// <summary>
    /// Registers Krackend testing services in an adapter-friendly shape for external test hosts.
    /// </summary>
    public static IServiceCollection AddKrackendTestingAdapter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKrackendTesting();
        services.TryAddSingleton<IKrackendTestingAdapter, KrackendTestingAdapter>();

        return services;
    }
}
