namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Defines timeout handling behaviors available in orchestration policies.
/// </summary>
public enum TimeoutBehavior
{
    /// <summary>
    /// Marks execution as failed after timeout.
    /// </summary>
    Fail,

    /// <summary>
    /// Waits for a configured grace period or further signal.
    /// </summary>
    Wait,

    /// <summary>
    /// Attempts reconciliation logic after timeout.
    /// </summary>
    Reconcile
}
