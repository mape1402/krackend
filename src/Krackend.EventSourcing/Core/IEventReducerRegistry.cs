namespace Krackend.EventSourcing.Core;

/// <summary>
/// Resolves reducer delegates for state/event combinations.
/// </summary>
public interface IEventReducerRegistry
{
    /// <summary>
    /// Registers a reducer delegate.
    /// </summary>
    IEventReducerRegistry Register<TState, TEvent>(Func<TState, TEvent, TState> reducer);

    /// <summary>
    /// Applies an event to a state instance.
    /// </summary>
    TState Apply<TState>(TState state, object @event);
}
