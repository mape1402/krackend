namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when a state CLR type cannot be snapshotted because no state schema is declared.
/// </summary>
public sealed class StateSchemaMissingException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StateSchemaMissingException"/> class.
    /// </summary>
    public StateSchemaMissingException(Type stateType)
        : base($"State type '{stateType.FullName}' must declare a state schema before it can be snapshotted.")
    {
        StateType = stateType;
    }

    /// <summary>
    /// Gets the state CLR type missing schema metadata.
    /// </summary>
    public Type StateType { get; }
}
