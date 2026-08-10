namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the on error policy values.
/// </summary>
public enum OnErrorPolicy
{
    /// <summary>
    /// Represents continue.
    /// </summary>
    Continue,

    /// <summary>
    /// Represents stop.
    /// </summary>
    Stop,
    
    /// <summary>
    /// Represents stop and compensate.
    /// </summary>
    StopAndCompensate
}
