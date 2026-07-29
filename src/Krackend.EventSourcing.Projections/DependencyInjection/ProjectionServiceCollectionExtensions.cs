using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Projections.DependencyInjection;

/// <summary>
/// Provides dependency injection extensions for Krackend event sourcing projections.
/// </summary>
public static class ProjectionServiceCollectionExtensions
{
    /// <summary>
    /// Registers Krackend event sourcing projection services.
    /// </summary>
    public static IServiceCollection AddKrackendEventSourcingProjections(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICheckpointStore, InMemoryCheckpointStore>();
        services.AddScoped<IProjectionRunner, ProjectionRunner>();

        var scanAssemblies = assemblies.Length > 0
            ? assemblies
            : Assembly.GetEntryAssembly() is { } entryAssembly
                ? [entryAssembly]
                : [];

        ProjectionAssemblyScanner.RegisterHandlers(services, scanAssemblies);

        return services;
    }
}
