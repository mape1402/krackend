using Krackend.EventSourcing.Streams;

namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Represents one event captured by the in-memory event sourcing test store.
/// </summary>
public sealed record EventSourcingTestEventEnvelope(
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
