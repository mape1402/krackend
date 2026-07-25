using Krackend.EventSourcing.Aggregates;

namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Provides Given/When/Then helpers for aggregate tests.
/// </summary>
public sealed class AggregateScenario<TAggregate>
    where TAggregate : IAggregateRoot, new()
{
    private readonly List<object> _history = [];
    private Action<TAggregate>? _when;

    /// <summary>
    /// Adds historical events to the scenario.
    /// </summary>
    public AggregateScenario<TAggregate> Given(params object[] events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _history.AddRange(events);
        return this;
    }

    /// <summary>
    /// Defines the action under test.
    /// </summary>
    public AggregateScenario<TAggregate> When(Action<TAggregate> action)
    {
        _when = action ?? throw new ArgumentNullException(nameof(action));
        return this;
    }

    /// <summary>
    /// Asserts the pending events raised by the aggregate.
    /// </summary>
    public void Then(params object[] expectedEvents)
    {
        ArgumentNullException.ThrowIfNull(expectedEvents);

        if (_when is null)
            throw new InvalidOperationException("A scenario requires a When action before Then.");

        var aggregate = new TAggregate();
        aggregate.LoadFromHistory(_history);

        _when(aggregate);

        var actualEvents = aggregate.PendingEvents.ToArray();

        if (actualEvents.Length != expectedEvents.Length)
            throw new InvalidOperationException($"Expected {expectedEvents.Length} event(s) but found {actualEvents.Length}.");

        for (var index = 0; index < expectedEvents.Length; index++)
        {
            if (!Equals(expectedEvents[index], actualEvents[index]))
            {
                throw new InvalidOperationException(
                    $"Expected event at index {index} to be '{expectedEvents[index]}' but found '{actualEvents[index]}'.");
            }
        }
    }
}
