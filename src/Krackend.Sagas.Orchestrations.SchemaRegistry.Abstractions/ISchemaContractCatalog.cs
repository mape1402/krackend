namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Provides provider-neutral discovery for schema contracts available to designers.
/// </summary>
public interface ISchemaContractCatalog
{
    /// <summary>
    /// Searches schema contracts across configured registry providers.
    /// </summary>
    /// <param name="request">Catalog search request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching schema contracts.</returns>
    Task<IReadOnlyCollection<SchemaContractCatalogItem>> SearchAsync(
        SchemaContractCatalogSearchRequest request,
        CancellationToken cancellationToken = default);
}
