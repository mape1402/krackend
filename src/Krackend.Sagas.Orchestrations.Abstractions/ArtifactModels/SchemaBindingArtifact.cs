namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

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
    bool StrictMode);
