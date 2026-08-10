namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable reconcile-on-timeout policy.
/// </summary>
public sealed record ReconcileTimeoutBehaviorPolicyArtifact(
    OrchestrationActionOnTimeout OrchestrationAction,
    RetryPolicyArtifact RetryPolicy) : ITimeoutBehaviorPolicyArtifact
{
    /// <summary>
    /// Gets reconcile behavior.
    /// </summary>
    public TimeoutBehavior Behavior => TimeoutBehavior.Reconcile;
}
