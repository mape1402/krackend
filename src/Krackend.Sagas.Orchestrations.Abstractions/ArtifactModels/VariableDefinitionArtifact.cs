namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable variable-definition contract.
/// </summary>
public sealed record VariableDefinitionArtifact(
    Id Id,
    string Key,
    string DisplayName,
    string Description,
    VariableScope Scope,
    VariableValueType ValueType,
    string DefaultValue,
    bool IsRequired,
    bool IsSensitive);
