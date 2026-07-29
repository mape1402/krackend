namespace Krackend.EventSourcing.Core;

/// <summary>
/// Creates the initial state used when a stream has no events or snapshots.
/// </summary>
public interface IInitialStateFactory<TState>
{
    /// <summary>
    /// Creates the initial state.
    /// </summary>
    ValueTask<TState> CreateAsync(CancellationToken cancellationToken = default);
}
