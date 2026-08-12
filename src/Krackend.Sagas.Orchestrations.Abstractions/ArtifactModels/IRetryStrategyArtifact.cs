namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable retry-strategy contract.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$artifactType")]
[JsonDerivedType(typeof(FixedRetryStrategyArtifact), "fixed")]
public interface IRetryStrategyArtifact
{
    /// <summary>
    /// Gets retry strategy type.
    /// </summary>
    RetryStrategyType Type { get; }
}
