namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable event-trigger channel contract.
/// </summary>
public sealed record EventTriggerChannelArtifact(
    SchemaBindingArtifact SchemaBinding,
    string Topic,
    SemanticVersion Version) : ITriggerChannelArtifact
{
    /// <summary>
    /// Gets event trigger type.
    /// </summary>
    public TriggerType TriggerType => TriggerType.Event;
}
