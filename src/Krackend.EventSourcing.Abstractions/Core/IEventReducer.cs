namespace Krackend.EventSourcing.Core;

/// <summary>
/// Applies one event to state through an object dispatcher.
/// </summary>
public interface IEventReducer
{
    /// <summary>
    /// Gets the state CLR type handled by this reducer.
    /// </summary>
    Type StateType { get; }

    /// <summary>
    /// Gets the event CLR type handled by this reducer.
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// Applies an event and returns the new state.
    /// </summary>
    object? Apply(object? state, object @event);
}

/// <summary>
/// Applies one event to a state instance.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventReducer<TState, in TEvent>
    : IEventReducer
{
    /// <inheritdoc />
    Type IEventReducer.StateType => typeof(TState);

    /// <inheritdoc />
    Type IEventReducer.EventType => typeof(TEvent);

    /// <inheritdoc />
    object? IEventReducer.Apply(object? state, object @event)
        => Apply((TState)state!, (TEvent)@event);

    /// <summary>
    /// Applies an event and returns the new state.
    /// </summary>
    TState Apply(TState state, TEvent @event);
}
