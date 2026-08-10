namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Default implementation of <see cref="IEventSourcingTestingAdapter"/>.
/// </summary>
public sealed class EventSourcingTestingAdapter : IEventSourcingTestingAdapter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcingTestingAdapter"/> class.
    /// </summary>
    public EventSourcingTestingAdapter(
        IEventSourcingTestEventStore eventStore,
        IEventSourcingTestAssertions assertions)
    {
        EventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        Assertions = assertions ?? throw new ArgumentNullException(nameof(assertions));
    }

    /// <inheritdoc />
    public IEventSourcingTestEventStore EventStore { get; }

    /// <inheritdoc />
    public IEventSourcingTestAssertions Assertions { get; }
}
