namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable wait-on-timeout policy.
/// </summary>
public sealed record WaitTimeoutBehaviorPolicyArtifact(
    OrchestrationActionOnTimeout OrchestrationAction,
    Duration WaitingTime) : ITimeoutBehaviorPolicyArtifact
{
    /// <summary>
    /// Gets wait behavior.
    /// </summary>
    public TimeoutBehavior Behavior => TimeoutBehavior.Wait;
}
