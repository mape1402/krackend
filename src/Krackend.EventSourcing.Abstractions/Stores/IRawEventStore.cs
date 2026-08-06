using Krackend.EventSourcing.Envelopes;

namespace Krackend.EventSourcing.Stores;

/// <summary>
/// Provides append and read operations for events that do not have CLR event types.
/// </summary>
public interface IRawEventStore
{
    /// <summary>
    /// Reads committed events for the specified stream from a version range.
    /// </summary>
    Task<IReadOnlyCollection<EventEnvelope>> ReadStreamAsync(
        string streamName,
        string streamId,
        long fromVersion,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current stream version without loading stream events.
    /// </summary>
    Task<long> GetCurrentVersionAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads raw events after a global position.
    /// </summary>
    Task<IReadOnlyCollection<EventEnvelope>> ReadFromAsync(
        string streamName,
        long afterGlobalPosition,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends raw events to the stream using optimistic concurrency.
    /// </summary>
    Task<IReadOnlyCollection<EventEnvelope>> AppendRawAsync(
        string streamName,
        string streamId,
        long expectedVersion,
        IReadOnlyCollection<RawEventData> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends raw events to the stream using the specified version precondition.
    /// </summary>
    Task<IReadOnlyCollection<EventEnvelope>> AppendRawAsync(
        string streamName,
        string streamId,
        ExpectedVersion expectedVersion,
        IReadOnlyCollection<RawEventData> events,
        CancellationToken cancellationToken = default);
}
