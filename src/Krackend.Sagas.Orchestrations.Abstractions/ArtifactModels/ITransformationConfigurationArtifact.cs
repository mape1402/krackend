namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable transformation-configuration contract.
/// </summary>
public interface ITransformationConfigurationArtifact
{
    /// <summary>
    /// Gets transformation engine associated with this configuration.
    /// </summary>
    EngineType Engine { get; }
}
