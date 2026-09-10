using Krackend.Sagas.Orchestrations.SchemaRegistry.Atlas;
using Krackend.Sagas.Orchestrations.SchemaRegistry.Atlas.Resolution;
using Krackend.Sagas.Orchestrations.SchemaRegistry.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.Atlas.DependencyInjection;

/// <summary>
/// Registers Atlas as a schema registry provider for Krackend orchestrations.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Atlas schema registry adapter.
    /// </summary>
    public static IServiceCollection AddKrackendAtlasSchemaRegistry(
        this IServiceCollection services,
        Action<AtlasSchemaRegistryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddKrackendSchemaRegistry();
        services.Configure(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISchemaContractResolver, AtlasSchemaContractResolver>());
        return services;
    }
}
