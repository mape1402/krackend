namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Represents an immutable schema contract snapshot embedded in an orchestration artifact.
/// </summary>
public sealed record SchemaContractSnapshotArtifact
{
    /// <summary>
    /// Gets the schema contract kind.
    /// </summary>
    public SchemaContractKind ContractKind { get; init; }

    /// <summary>
    /// Gets the configured schema registry provider id.
    /// </summary>
    public string RegistryProviderId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the configured schema registry provider key.
    /// </summary>
    public string RegistryProviderKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the external contract identifier.
    /// </summary>
    public string ContractId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the logical contract key.
    /// </summary>
    public string ContractKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the contract version.
    /// </summary>
    public string ContractVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the schema format.
    /// </summary>
    public string SchemaFormat { get; init; } = "ButterMorph";

    /// <summary>
    /// Gets the schema JSON document.
    /// </summary>
    public string SchemaJson { get; init; } = "{}";

    /// <summary>
    /// Gets the schema content hash.
    /// </summary>
    public string ContentHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the provider-specific source artifact id.
    /// </summary>
    public string SourceArtifactId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the provider that resolved the snapshot.
    /// </summary>
    public string ResolvedBy { get; init; } = string.Empty;

    /// <summary>
    /// Gets when the schema was resolved.
    /// </summary>
    public DateTimeOffset ResolvedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
