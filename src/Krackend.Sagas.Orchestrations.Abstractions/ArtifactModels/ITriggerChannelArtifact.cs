namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable trigger-channel contract.
/// </summary>
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
