namespace Krackend.EventSourcing.Envelopes;

/// <summary>
/// Creates identifiers for events persisted in an event store.
/// </summary>
public interface IEventIdFactory
{
    /// <summary>
    /// Creates an identifier for the event described by the specified context.
    /// </summary>
    string Create(EventIdFactoryContext context);
}
