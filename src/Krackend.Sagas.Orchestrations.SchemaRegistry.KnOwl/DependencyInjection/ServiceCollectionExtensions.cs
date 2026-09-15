using Krackend.Sagas.Orchestrations.SchemaRegistry.DependencyInjection;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.DependencyInjection;

/// <summary>
/// Registers the KnOwl Control Plane schema registry adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the KnOwl Control Plane schema registry adapter.
    /// </summary>
    public static IServiceCollection AddKrackendKnOwlSchemaRegistry(
        this IServiceCollection services,
        Action<KnOwlSchemaRegistryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddKrackendSchemaRegistry();
        services.Configure(configure);
        services.TryAddSingleton<IKnOwlControlPlaneContractCatalogClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<KnOwlSchemaRegistryOptions>>().Value;
            var httpClient = new HttpClient
            {
                BaseAddress = EnsureTrailingSlash(options.BaseUri),
                Timeout = options.Timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : options.Timeout
            };

            return new KnOwlControlPlaneContractCatalogHttpClient(httpClient);
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISchemaContractResolver, KnOwlSchemaContractResolver>());
        return services;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (uri is null)
        {
            return null;
        }

        var text = uri.ToString();
        return text.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(text + "/", UriKind.Absolute);
    }
}
