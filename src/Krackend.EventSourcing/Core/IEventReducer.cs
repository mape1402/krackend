namespace Krackend.EventSourcing.Core;

/// <summary>
/// Applies one event to a state instance.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventReducer<TState, in TEvent>
{
    /// <summary>
    /// Applies an event and returns the new state.
    /// </summary>
    TState Apply(TState state, TEvent @event);
}
