namespace Krackend.EventSourcing.Envelopes;

using Krackend.EventSourcing.Stores;

/// <summary>
/// Creates committed event envelopes from domain events.
/// </summary>
public interface IEventEnvelopeFactory
{
    /// <summary>
    /// Creates envelopes for a commit operation.
    /// </summary>
    IReadOnlyCollection<EventEnvelope> Create(
        string streamName,
        string streamId,
        string? streamType,
        long expectedVersion,
        IReadOnlyCollection<object> events);

    /// <summary>
    /// Creates envelopes for a raw commit operation.
    /// </summary>
    IReadOnlyCollection<EventEnvelope> CreateRaw(
        string streamName,
        string streamId,
        string? streamType,
        long expectedVersion,
        IReadOnlyCollection<RawEventData> events);
}
