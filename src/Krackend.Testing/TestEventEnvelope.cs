using Krackend.EventSourcing.Streams;

namespace Krackend.Testing;

/// <summary>
/// Represents one event captured by the in-memory test event store.
/// </summary>
public sealed record TestEventEnvelope(
    Guid EventId,
    EventStreamReference Stream,
    long StreamVersion,
    long GlobalPosition,
    string EventType,
    Type EventClrType,
    object Event,
    string SerializedPayload,
    IReadOnlyDictionary<string, object?> Metadata,
    DateTimeOffset OccurredAt);
