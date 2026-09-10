namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Identifies one schema contract in a provider-neutral registry.
/// </summary>
public sealed record SchemaContractReference
{
    /// <summary>
    /// Gets the local provider identifier configured by the orchestration designer.
    /// </summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the provider key or logical name.
    /// </summary>
    public string ProviderKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the external contract identifier when the provider exposes one.
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
    /// Gets the orchestration payload role for the contract.
    /// </summary>
    public SchemaContractKind Kind { get; init; }

    /// <summary>
    /// Gets whether consumers should reject payloads that do not exactly match the contract.
    /// </summary>
    public bool StrictMode { get; init; }

    /// <summary>
    /// Gets whether validation is enabled for this binding.
    /// </summary>
    public bool IsValidationEnabled { get; init; }
}
