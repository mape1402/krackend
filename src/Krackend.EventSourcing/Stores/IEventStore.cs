using Krackend.EventSourcing.Envelopes;

namespace Krackend.EventSourcing.Stores;

/// <summary>
/// Provides append and stream read operations for committed events.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Loads all committed events for the specified stream.
    /// </summary>
    Task<IReadOnlyCollection<EventEnvelope>> LoadAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends new events to the stream using optimistic concurrency.
    /// </summary>
    Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
        string streamName,
        string streamId,
        long expectedVersion,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default);
}
