namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// Represents a persisted event store row.
/// </summary>
public sealed class EventStoreRecord
{
    /// <summary>
    /// Gets or sets the event identifier.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Gets or sets the logical stream name.
    /// </summary>
    public string StreamName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream identifier.
    /// </summary>
    public string StreamId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional stream type.
    /// </summary>
    public string? StreamType { get; set; }

    /// <summary>
    /// Gets or sets the stream version.
    /// </summary>
    public long StreamVersion { get; set; }

    /// <summary>
    /// Gets or sets the global ordering position.
    /// </summary>
    public long GlobalPosition { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event version.
    /// </summary>
    public int EventVersion { get; set; }

    /// <summary>
    /// Gets or sets the occurrence timestamp.
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// Gets or sets the serialized payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets serialized metadata.
    /// </summary>
    public string? Metadata { get; set; }
}
