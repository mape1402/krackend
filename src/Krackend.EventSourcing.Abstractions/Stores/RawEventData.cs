using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Stores;

/// <summary>
/// Represents an event payload that can be appended without a CLR event type.
/// </summary>
public sealed record RawEventData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RawEventData"/> record.
    /// </summary>
    /// <param name="eventType">The logical event type name stored in the envelope.</param>
    /// <param name="eventSchemaVersion">The event payload schema version.</param>
    /// <param name="payload">The raw JSON payload to store.</param>
    /// <param name="metadata">The optional raw JSON metadata to store.</param>
    public RawEventData(
        string eventType,
        SemanticVersion eventSchemaVersion,
        string payload,
        string? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        EventType = eventType;
        EventSchemaVersion = eventSchemaVersion == default
            ? SemanticVersion.Default
            : eventSchemaVersion;
        Payload = payload;
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the logical event type name stored in the envelope.
    /// </summary>
    public string EventType { get; init; }

    /// <summary>
    /// Gets the event payload schema version.
    /// </summary>
    public SemanticVersion EventSchemaVersion { get; init; }

    /// <summary>
    /// Gets the raw JSON payload to store.
    /// </summary>
    public string Payload { get; init; }

    /// <summary>
    /// Gets the optional raw JSON metadata to store.
    /// </summary>
    public string? Metadata { get; init; }
}
