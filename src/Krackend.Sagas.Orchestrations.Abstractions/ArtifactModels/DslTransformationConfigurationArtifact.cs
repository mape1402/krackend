namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable DSL transformation configuration.
/// </summary>
public sealed record DslTransformationConfigurationArtifact : ITransformationConfigurationArtifact
{
    /// <summary>
    /// Gets DSL engine.
    /// </summary>
    public EngineType Engine => EngineType.DSL;
}
