namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the trigger type values.
/// </summary>
public enum TriggerType
{
    /// <summary>
    /// Represents an event.
    /// </summary>
    Event,
    /// <summary>
    /// Represents http.
    /// </summary>
    Http,
    /// <summary>
    /// Represents scheduled.
    /// </summary>
    Scheduled,
    /// <summary>
    /// Represents manual.
    /// </summary>
    Manual,
    /// <summary>
    /// Represents custom.
    /// </summary>
    Custom
}
