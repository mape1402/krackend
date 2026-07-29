namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when no reducer exists for a state and event pair.
/// </summary>
public sealed class EventReducerNotRegisteredException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventReducerNotRegisteredException"/> class.
    /// </summary>
    public EventReducerNotRegisteredException(Type stateType, Type eventClrType)
        : base($"No reducer registered for state '{stateType.FullName}' and event '{eventClrType.FullName}'.")
    {
        StateType = stateType;
        EventClrType = eventClrType;
    }

    /// <summary>
    /// Gets the state CLR type.
    /// </summary>
    public Type StateType { get; }

    /// <summary>
    /// Gets the event CLR type.
    /// </summary>
    public Type EventClrType { get; }
}
