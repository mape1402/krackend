namespace Krackend.EventSourcing.Envelopes;

using Krackend.EventSourcing.Contracts;

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
    SemanticVersion EventSchemaVersion { get; }

    /// <summary>
    /// Gets the time when the event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Gets the identifier that correlates all work in the same flow.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the identifier of the request, message, or event that caused this event.
    /// </summary>
    string? CausationId { get; }

    /// <summary>
    /// Gets the user identifier associated with the event.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the tenant identifier associated with the event.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets the source component or channel that originated the event.
    /// </summary>
    string? Source { get; }

    /// <summary>
    /// Gets the serialized event payload.
    /// </summary>
    string Payload { get; }

    /// <summary>
    /// Gets the serialized event metadata.
    /// </summary>
    string? Metadata { get; }
}
