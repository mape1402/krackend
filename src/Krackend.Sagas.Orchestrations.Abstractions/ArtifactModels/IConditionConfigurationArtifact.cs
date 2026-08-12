namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable condition-configuration contract.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$artifactType")]
[JsonDerivedType(typeof(DslConditionConfigurationArtifact), "dsl")]
public interface IConditionConfigurationArtifact
{
    /// <summary>
    /// Gets condition engine associated with this configuration.
    /// </summary>
    EngineType Engine { get; }
}
