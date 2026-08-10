namespace Krackend.Sagas.Orchestrations.Design.Core.TimeoutBehaviorPolicies;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a timeout behavior policy that attempts reconciliation logic after a timeout event.
/// </summary>
public class ReconcileTimeoutBehaviorPolicy : ITimeoutBehaviorPolicy
{
    /// <summary>
    /// Gets the timeout behavior represented by this policy.
    /// </summary>
    public TimeoutBehavior Behavior => TimeoutBehavior.Reconcile;

    /// <summary>
    /// Gets or sets the orchestration action to apply after reconciliation attempts are completed.
    /// </summary>
    public OrchestrationActionOnTimeout OrchestrationAction { get; set; }

    /// <summary>
    /// Gets or sets the retry policy used during reconciliation.
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; }
}
