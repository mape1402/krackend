namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Defines the channel contract used by trigger bindings at design time.
/// </summary>
public interface ITriggerChannel
{
    /// <summary>
    /// Gets the trigger type handled by this channel.
    /// </summary>
    TriggerType TriggerType { get; }

    /// <summary>
    /// Gets or sets the schema binding used to validate payloads received through this channel.
    /// </summary>
    SchemaBinding SchemaBinding { get; set; }
}
