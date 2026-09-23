namespace Krackend.Sagas.Orchestrations.SchemaRegistry.Resolution;

/// <summary>
/// Searches schema contracts across registered schema registry catalog providers.
/// </summary>
public sealed class DefaultSchemaContractCatalog : ISchemaContractCatalog
{
    private const int DefaultTake = 25;
    private const int MaxTake = 100;
    private readonly IEnumerable<ISchemaContractCatalogProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSchemaContractCatalog"/> class.
    /// </summary>
    /// <param name="providers">Registered schema contract catalog providers.</param>
    public DefaultSchemaContractCatalog(IEnumerable<ISchemaContractCatalogProvider> providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<SchemaContractCatalogItem>> SearchAsync(
        SchemaContractCatalogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        request ??= new SchemaContractCatalogSearchRequest();

        var take = NormalizeTake(request.Take);
        var providers = GetTargetProviders(request.ProviderKey).ToArray();
        if (providers.Length == 0)
        {
            return Array.Empty<SchemaContractCatalogItem>();
        }

        var results = new List<SchemaContractCatalogItem>();
        foreach (var provider in providers)
        {
            if (results.Count >= take)
            {
                break;
            }

            var providerResults = await provider.SearchAsync(request with { Take = take - results.Count }, cancellationToken);
            results.AddRange(providerResults ?? Array.Empty<SchemaContractCatalogItem>());
        }

        var unique = new List<SchemaContractCatalogItem>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in results)
        {
            var key = $"{item.ProviderKey}|{item.ContractKind}|{item.ContractKey}|{item.ContractVersion}";
            if (keys.Add(key))
            {
                unique.Add(item);
            }
        }

        return unique.Take(take).ToArray();
    }

    private IEnumerable<ISchemaContractCatalogProvider> GetTargetProviders(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return _providers;
        }

        return _providers.Where(provider =>
            string.Equals(provider.ProviderKey, providerKey.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static int NormalizeTake(int take)
    {
        if (take <= 0)
        {
            return DefaultTake;
        }

        return Math.Min(take, MaxTake);
    }

}
