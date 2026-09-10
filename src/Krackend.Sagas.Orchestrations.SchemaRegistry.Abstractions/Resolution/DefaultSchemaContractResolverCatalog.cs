using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.Resolution;

/// <summary>
/// Selects schema contract resolvers from the current service provider.
/// </summary>
public sealed class DefaultSchemaContractResolverCatalog : ISchemaContractResolverCatalog
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NoopSchemaContractResolver _noopResolver = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSchemaContractResolverCatalog"/> class.
    /// </summary>
    public DefaultSchemaContractResolverCatalog(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public ISchemaContractResolver GetResolver(SchemaContractReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var providerKey = string.IsNullOrWhiteSpace(reference.ProviderKey)
            ? reference.ProviderId
            : reference.ProviderKey;

        return _serviceProvider
            .GetServices<ISchemaContractResolver>()
            .FirstOrDefault(resolver => string.Equals(resolver.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase))
            ?? _noopResolver;
    }
}
