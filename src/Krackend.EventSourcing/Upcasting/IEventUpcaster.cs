namespace Krackend.EventSourcing.Upcasting;

/// <summary>
/// Upcasts persisted event payloads from older versions.
/// </summary>
public interface IEventUpcaster
{
    /// <summary>
    /// Gets the persisted event type handled by this upcaster.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Gets the source event schema version.
    /// </summary>
    string FromSchemaVersion { get; }

    /// <summary>
    /// Gets the target event schema version.
    /// </summary>
    string ToSchemaVersion { get; }

    /// <summary>
    /// Converts a payload into the next event version.
    /// </summary>
    string Upcast(string payload);
}
