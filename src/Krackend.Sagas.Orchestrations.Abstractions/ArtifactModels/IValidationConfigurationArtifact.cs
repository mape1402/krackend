namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Defines an immutable validation configuration carried by an orchestration artifact.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$artifactType")]
[JsonDerivedType(typeof(DslValidationConfigurationArtifact), "dsl")]
public interface IValidationConfigurationArtifact
{
    /// <summary>
    /// Gets the engine required to execute this validation configuration.
    /// </summary>
    EngineType Engine { get; }
}
