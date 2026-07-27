namespace Krackend.EventSourcing.Core;

/// <summary>
/// Produces domain events from a command and the current state.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface IEventDecider<in TState, in TCommand>
{
    /// <summary>
    /// Decides which events should be committed.
    /// </summary>
    ValueTask<IReadOnlyCollection<object>> DecideAsync(
        TState state,
        TCommand command,
        CancellationToken cancellationToken = default);
}
