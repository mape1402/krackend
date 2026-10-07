namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Represents the current operational state of an orchestration runtime replica.
/// </summary>
public enum RuntimeOperationalState
{
    /// <summary>
    /// Runtime dependencies are available and all operations can be admitted.
    /// </summary>
    Healthy,

    /// <summary>
    /// Optional dependencies are unavailable but durable runtime processing can continue.
    /// </summary>
    Degraded,

    /// <summary>
    /// A critical dependency is unavailable and the runtime must not accept or mutate orchestration work.
    /// </summary>
    Closed,

    /// <summary>
    /// Critical dependencies are available again and recovery must run before intake reopens.
    /// </summary>
    Recovering
}
