namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Represents one schema contract available from a registry catalog.
/// </summary>
public sealed record SchemaContractCatalogItem
{
    /// <summary>
    /// Gets the provider key that owns the contract.
    /// </summary>
    public string ProviderKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the provider-native contract identifier.
    /// </summary>
    public string ContractId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the logical contract key.
    /// </summary>
    public string ContractKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the semantic or provider-native contract version.
    /// </summary>
    public string ContractVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the orchestration payload role represented by the contract.
    /// </summary>
    public SchemaContractKind ContractKind { get; init; }

    /// <summary>
    /// Gets the schema content hash when the provider exposes one.
    /// </summary>
    public string ContentHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets a human-readable label for design-time selection.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;
}
