using Krackend.EventSourcing.Core;

namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Provides reducer test helpers.
/// </summary>
public static class ReducerTest
{
    /// <summary>
    /// Applies an event to a state using a reducer.
    /// </summary>
    public static TState Apply<TState, TEvent>(
        IEventReducer<TState, TEvent> reducer,
        TState state,
        TEvent @event)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        ArgumentNullException.ThrowIfNull(@event);

        return reducer.Apply(state, @event);
    }

    /// <summary>
    /// Applies an event to a state using a reducer delegate.
    /// </summary>
    public static TState Apply<TState, TEvent>(
        TState state,
        TEvent @event,
        Func<TState, TEvent, TState> reducer)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(reducer);

        return reducer(state, @event);
    }
}
