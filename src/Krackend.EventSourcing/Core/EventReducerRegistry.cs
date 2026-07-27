namespace Krackend.EventSourcing.Core;

/// <summary>
/// In-memory reducer registry backed by typed delegates.
/// </summary>
public sealed class EventReducerRegistry : IEventReducerRegistry
{
    private readonly Dictionary<ReducerKey, Func<object?, object, object?>> _reducers = [];

    /// <inheritdoc />
    public IEventReducerRegistry Register<TState, TEvent>(Func<TState, TEvent, TState> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);

        _reducers[new ReducerKey(typeof(TState), typeof(TEvent))] =
            (state, @event) => reducer((TState)state!, (TEvent)@event);

        return this;
    }

    /// <inheritdoc />
    public IEventReducerRegistry Register(IEventReducer reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);

        _reducers[new ReducerKey(reducer.StateType, reducer.EventType)] = reducer.Apply;
        return this;
    }

    /// <inheritdoc />
    public TState Apply<TState>(TState state, object @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var key = new ReducerKey(typeof(TState), @event.GetType());

        if (!_reducers.TryGetValue(key, out var reducer))
            return state;

        return (TState)reducer(state, @event)!;
    }

    private readonly record struct ReducerKey(Type StateType, Type EventType);
}
