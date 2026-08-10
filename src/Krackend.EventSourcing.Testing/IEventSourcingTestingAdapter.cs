namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Exposes event sourcing testing services for external test host integrations.
/// </summary>
public interface IEventSourcingTestingAdapter
{
    /// <summary>
    /// Gets the in-memory test event store.
    /// </summary>
    IEventSourcingTestEventStore EventStore { get; }

    /// <summary>
    /// Gets event sourcing assertions backed by the in-memory test event store.
    /// </summary>
    IEventSourcingTestAssertions Assertions { get; }
}
