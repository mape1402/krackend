namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Discovers schema contracts from one configured schema registry provider.
/// </summary>
public interface ISchemaContractCatalogProvider
{
    /// <summary>
    /// Gets the provider key handled by this catalog provider.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Searches schema contracts exposed by this provider.
    /// </summary>
    /// <param name="request">Catalog search request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching schema contracts.</returns>
    Task<IReadOnlyCollection<SchemaContractCatalogItem>> SearchAsync(
        SchemaContractCatalogSearchRequest request,
        CancellationToken cancellationToken = default);
}
