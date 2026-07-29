namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when an event CLR type cannot be registered because no schema name is available.
/// </summary>
public sealed class EventSchemaMissingException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventSchemaMissingException"/> class.
    /// </summary>
    public EventSchemaMissingException(Type eventClrType)
        : base($"Event type '{eventClrType.FullName}' must declare an event schema or be registered with an explicit event type name.")
    {
        EventClrType = eventClrType;
    }

    /// <summary>
    /// Gets the event CLR type that could not be registered.
    /// </summary>
    public Type EventClrType { get; }
}
