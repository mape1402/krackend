namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable transformation contract.
/// </summary>
public sealed record TransformationArtifact(
    EngineType Engine,
    ITransformationConfigurationArtifact Configuration)
{
    /// <summary>
    /// Gets whether the transformation is enabled.
    /// </summary>
    public bool IsEnabled { get; init; }
}
