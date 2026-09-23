namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Describes a schema contract catalog search.
/// </summary>
public sealed record SchemaContractCatalogSearchRequest
{
    /// <summary>
    /// Gets the provider key to query. When empty, every configured provider may be queried.
    /// </summary>
    public string ProviderKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the contract kind filter.
    /// </summary>
    public SchemaContractKind ContractKind { get; init; } = SchemaContractKind.Unspecified;

    /// <summary>
    /// Gets free text used to filter by key, version, or provider-native id.
    /// </summary>
    public string SearchText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the maximum number of contracts to return.
    /// </summary>
    public int Take { get; init; } = 25;
}
