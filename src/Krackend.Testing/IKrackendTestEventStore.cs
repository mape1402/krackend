using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Streams;

namespace Krackend.Testing;

/// <summary>
/// Provides an in-memory event store for testing event sourcing flows without a real event store.
/// </summary>
public interface IKrackendTestEventStore
{
    /// <summary>
    /// Appends events to a stream without an optimistic concurrency precondition.
    /// </summary>
    Task<IReadOnlyCollection<TestEventEnvelope>> AppendAsync(
        EventStreamReference stream,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends events to a stream with metadata and without an optimistic concurrency precondition.
    /// </summary>
    Task<IReadOnlyCollection<TestEventEnvelope>> AppendAsync(
        EventStreamReference stream,
        IReadOnlyCollection<object> events,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends events to a stream using the specified optimistic concurrency precondition.
    /// </summary>
    Task<IReadOnlyCollection<TestEventEnvelope>> AppendAsync(
        EventStreamReference stream,
        ExpectedVersion expectedVersion,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends events to a stream using the specified optimistic concurrency precondition and metadata.
    /// </summary>
    Task<IReadOnlyCollection<TestEventEnvelope>> AppendAsync(
        EventStreamReference stream,
        ExpectedVersion expectedVersion,
        IReadOnlyCollection<object> events,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads events from a stream in stream-version order.
    /// </summary>
    Task<IReadOnlyCollection<TestEventEnvelope>> ReadAsync(
        EventStreamReference stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all events in append order.
    /// </summary>
    Task<IReadOnlyCollection<TestEventEnvelope>> ReadAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the next append to the specified stream fail with an optimistic concurrency conflict.
    /// </summary>
    void FailNextAppendWithConcurrencyConflict(EventStreamReference stream);
}
