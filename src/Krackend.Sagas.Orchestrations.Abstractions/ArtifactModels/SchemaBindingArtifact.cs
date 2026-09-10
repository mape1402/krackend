namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Represents an immutable schema-binding contract.
/// </summary>
public sealed record SchemaBindingArtifact(
    Id Id,
    ElementType ElementType,
    Id ElementId,
    Id ContractId,
    string ContractKey,
    SemanticVersion ContractVersion,
    Id RegistryProviderId,
    bool StrictMode)
{
    /// <summary>
    /// Gets whether schema validation is enabled.
    /// </summary>
    public bool IsValidationEnabled { get; init; }

    /// <summary>
    /// Gets the provider key used to resolve this binding.
    /// </summary>
    public string RegistryProviderKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the orchestration payload role represented by this binding.
    /// </summary>
    public SchemaContractKind ContractKind { get; init; } = SchemaContractKind.Unspecified;

    /// <summary>
    /// Gets the immutable schema snapshot captured when the artifact was built.
    /// </summary>
    public SchemaContractSnapshotArtifact Snapshot { get; init; }
}
