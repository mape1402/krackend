using Krackend.EventSourcing.Core;

namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Provides decider test helpers.
/// </summary>
public static class DeciderTest
{
    /// <summary>
    /// Executes a decider and returns the decided events.
    /// </summary>
    public static ValueTask<IReadOnlyCollection<object>> DecideAsync<TState, TCommand>(
        IEventDecider<TState, TCommand> decider,
        TState state,
        TCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decider);
        ArgumentNullException.ThrowIfNull(command);

        return decider.DecideAsync(state, command, cancellationToken);
    }
}
