namespace Krackend.EventSourcing.Envelopes;

/// <summary>
/// Represents the storage envelope that surrounds a persisted domain event.
/// </summary>
public interface IEventEnvelope
{
    /// <summary>
    /// Gets the unique event identifier.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the logical store or stream group name.
    /// </summary>
    string StreamName { get; }

    /// <summary>
    /// Gets the aggregate or business stream identifier.
    /// </summary>
    string StreamId { get; }

    /// <summary>
    /// Gets the optional stream type.
    /// </summary>
    string? StreamType { get; }

    /// <summary>
    /// Gets the version assigned to this event inside the stream.
    /// </summary>
    long StreamVersion { get; }

    /// <summary>
    /// Gets the global persisted ordering position when available.
    /// </summary>
    long? GlobalPosition { get; }

    /// <summary>
    /// Gets the registered event type name.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Gets the schema version of the event payload.
    /// </summary>
    int EventVersion { get; }

    /// <summary>
    /// Gets the time when the event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Gets the serialized event payload.
    /// </summary>
    string Payload { get; }

    /// <summary>
    /// Gets the serialized event metadata.
    /// </summary>
    string? Metadata { get; }
}
