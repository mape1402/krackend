namespace Krackend.EventSourcing.Envelopes;

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
}
