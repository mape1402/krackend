using Krackend.EventSourcing.Aggregates;

namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Entry point for event sourcing test scenarios.
/// </summary>
public static class EventSourcingScenario
{
    /// <summary>
    /// Creates an aggregate scenario.
    /// </summary>
    public static AggregateScenario<TAggregate> For<TAggregate>()
        where TAggregate : IAggregateRoot, new()
        => new();
}
