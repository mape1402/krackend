namespace Krackend.EventSourcing.Aggregates;

/// <summary>
/// Represents an aggregate that can be rehydrated from events and expose pending events.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>
    /// Gets the stream identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the current stream version.
    /// </summary>
    long Version { get; }

    /// <summary>
    /// Gets events raised but not yet committed.
    /// </summary>
    IReadOnlyCollection<object> PendingEvents { get; }

    /// <summary>
    /// Loads committed events into the aggregate.
    /// </summary>
    void LoadFromHistory(IEnumerable<object> events);

    /// <summary>
    /// Clears pending events after a successful append.
    /// </summary>
    void ClearPendingEvents();
}
