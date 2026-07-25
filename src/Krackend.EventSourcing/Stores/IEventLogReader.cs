using Krackend.EventSourcing.Envelopes;

namespace Krackend.EventSourcing.Stores;

/// <summary>
/// Reads committed events by global position for subscriptions and projections.
/// </summary>
public interface IEventLogReader
{
    /// <summary>
    /// Reads events after a global position.
    /// </summary>
    Task<IReadOnlyCollection<EventEnvelope>> ReadFromAsync(
        string streamName,
        long afterGlobalPosition,
        int maxCount,
        CancellationToken cancellationToken = default);
}
