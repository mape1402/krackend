namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable DSL condition configuration.
/// </summary>
public sealed record DslConditionConfigurationArtifact(Expression Expression) : IConditionConfigurationArtifact
{
    /// <summary>
    /// Gets DSL engine.
    /// </summary>
    public EngineType Engine => EngineType.DSL;
}
