namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an event-driven trigger channel bound to a topic and schema contract.
/// </summary>
public class EventTriggerChannel : ITriggerChannel
{
    /// <summary>
    /// Gets the trigger type supported by this channel.
    /// </summary>
    public TriggerType TriggerType => TriggerType.Event;

    /// <summary>
    /// Gets or sets the schema binding used to validate incoming event payloads.
    /// </summary>
    public SchemaBinding SchemaBinding { get; set; }

    /// <summary>
    /// Gets or sets whether incoming event payload validation is enabled.
    /// </summary>
    public bool HasSchemaValidation { get; set; }

    /// <summary>
    /// Gets or sets the validation executed against incoming event payloads.
    /// </summary>
    public ValidationDefinition Validation { get; set; }

    /// <summary>
    /// Gets or sets whether incoming event validation is enabled.
    /// </summary>
    public bool HasValidation { get; set; }

    /// <summary>
    /// Gets or sets the topic used to subscribe or publish orchestration trigger events.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Gets or sets the event contract version accepted by this channel.
    /// </summary>
    public SemanticVersion Version { get; set; }
}
