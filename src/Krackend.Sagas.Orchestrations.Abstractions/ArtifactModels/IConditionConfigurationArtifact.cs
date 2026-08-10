namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable condition-configuration contract.
/// </summary>
public interface IConditionConfigurationArtifact
{
    /// <summary>
    /// Gets condition engine associated with this configuration.
    /// </summary>
    EngineType Engine { get; }
}
