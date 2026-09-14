namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable trigger-channel contract.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$artifactType")]
[JsonDerivedType(typeof(EventTriggerChannelArtifact), "event")]
public interface ITriggerChannelArtifact
{
    /// <summary>
    /// Gets trigger type associated with this channel.
    /// </summary>
    TriggerType TriggerType { get; }

    /// <summary>
    /// Gets schema binding associated with this channel.
    /// </summary>
    SchemaBindingArtifact SchemaBinding { get; }
}
