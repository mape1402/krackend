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
    /// Gets the source event version.
    /// </summary>
    int FromVersion { get; }

    /// <summary>
    /// Gets the target event version.
    /// </summary>
    int ToVersion { get; }

    /// <summary>
    /// Converts a payload into the next event version.
    /// </summary>
    string Upcast(string payload);
}
