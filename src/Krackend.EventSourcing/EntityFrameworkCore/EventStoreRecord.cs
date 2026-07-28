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
    /// Gets or sets the identifier that correlates all work in the same flow.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the request, message, or event that caused this event.
    /// </summary>
    public string? CausationId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier associated with the event.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier associated with the event.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the source component or channel that originated the event.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the serialized payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets serialized metadata.
    /// </summary>
    public string? Metadata { get; set; }
}
