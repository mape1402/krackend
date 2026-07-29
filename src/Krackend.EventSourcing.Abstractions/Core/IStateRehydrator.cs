namespace Krackend.EventSourcing.Core;

/// <summary>
/// Rehydrates state from committed events.
/// </summary>
public interface IStateRehydrator
{
    /// <summary>
    /// Loads a stream and reduces its events into state using the configured initial state factory.
    /// </summary>
    Task<EventSourcedState<TState>> RehydrateAsync<TState>(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a stream and reduces its events into state.
    /// </summary>
    Task<EventSourcedState<TState>> RehydrateAsync<TState>(
        string streamName,
        string streamId,
        TState initialState,
        CancellationToken cancellationToken = default);
}
