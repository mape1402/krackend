namespace Krackend.EventSourcing.Core;

/// <summary>
/// Executes a command through event sourcing.
/// </summary>
public interface IEventSourcedApplicationService<TState, in TCommand>
{
    /// <summary>
    /// Rehydrates state, decides events, and appends them.
    /// </summary>
    Task<EventSourcingExecutionResult<TState>> ExecuteAsync(
        string streamName,
        string streamId,
        TState initialState,
        TCommand command,
        CancellationToken cancellationToken = default);
}
