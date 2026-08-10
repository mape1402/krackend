namespace Krackend.Sagas.Orchestrations.Design.Core.TimeoutBehaviorPolicies;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a timeout behavior policy that waits for a configured window before continuing orchestration flow.
/// </summary>
public class WaitTimeoutBehaviorPolicy : ITimeoutBehaviorPolicy
{
    /// <summary>
    /// Gets the timeout behavior represented by this policy.
    /// </summary>
    public TimeoutBehavior Behavior => TimeoutBehavior.Wait;

    /// <summary>
    /// Gets or sets the orchestration action to apply after the waiting window expires.
    /// </summary>
    public OrchestrationActionOnTimeout OrchestrationAction { get; set; }

    /// <summary>
    /// Gets or sets the waiting duration applied after timeout.
    /// </summary>
    public Duration WaitingTime { get; set; }
}
