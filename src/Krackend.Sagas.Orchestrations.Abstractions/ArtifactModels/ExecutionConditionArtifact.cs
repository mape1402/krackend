namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable execution-condition contract.
/// </summary>
public sealed record ExecutionConditionArtifact(
    EngineType Engine,
    IConditionConfigurationArtifact Configuration);
