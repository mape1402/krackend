namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when an event-sourced service needs an initial state but no factory was configured.
/// </summary>
public sealed class InitialStateNotConfiguredException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InitialStateNotConfiguredException"/> class.
    /// </summary>
    public InitialStateNotConfiguredException(Type stateType)
        : base($"Initial state for '{stateType.FullName}' is not configured. Register it with AddEventSourcedInitialState.")
    {
        StateType = stateType;
    }

    /// <summary>
    /// Gets the state type missing initial state configuration.
    /// </summary>
    public Type StateType { get; }
}
