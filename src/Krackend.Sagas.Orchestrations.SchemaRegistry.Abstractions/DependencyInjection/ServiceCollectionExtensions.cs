using Krackend.Sagas.Orchestrations.SchemaRegistry.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.DependencyInjection;

/// <summary>
/// Registers provider-neutral schema registry services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default schema registry resolver catalog and local snapshot resolver.
    /// </summary>
    public static IServiceCollection AddKrackendSchemaRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISchemaContractSnapshotStore, InMemorySchemaContractSnapshotStore>();
        services.TryAddSingleton<ISchemaContractResolverCatalog, DefaultSchemaContractResolverCatalog>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISchemaContractResolver, SnapshotSchemaContractResolver>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISchemaContractResolver, NoopSchemaContractResolver>());
        return services;
    }
}
