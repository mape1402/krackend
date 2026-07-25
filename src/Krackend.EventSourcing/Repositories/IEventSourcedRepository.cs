using Krackend.EventSourcing.Aggregates;

namespace Krackend.EventSourcing.Repositories;

/// <summary>
/// Loads and saves event sourced aggregates.
/// </summary>
public interface IEventSourcedRepository<TAggregate>
    where TAggregate : IAggregateRoot
{
    /// <summary>
    /// Loads an aggregate by stream identifier.
    /// </summary>
    Task<TAggregate> LoadAsync(string streamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the aggregate pending events.
    /// </summary>
    Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}
