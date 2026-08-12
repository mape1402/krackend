namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable transformation-configuration contract.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$artifactType")]
[JsonDerivedType(typeof(DslTransformationConfigurationArtifact), "dsl")]
public interface ITransformationConfigurationArtifact
{
    /// <summary>
    /// Gets transformation engine associated with this configuration.
    /// </summary>
    EngineType Engine { get; }
}
