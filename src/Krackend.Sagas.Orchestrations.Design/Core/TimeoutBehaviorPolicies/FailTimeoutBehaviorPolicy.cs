namespace Krackend.Sagas.Orchestrations.Design.Core.TimeoutBehaviorPolicies;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a timeout behavior policy that fails execution when the timeout is reached.
/// </summary>
public class FailTimeoutBehaviorPolicy : ITimeoutBehaviorPolicy
{
    /// <summary>
    /// Gets the timeout behavior represented by this policy.
    /// </summary>
    public TimeoutBehavior Behavior => TimeoutBehavior.Fail;

    /// <summary>
    /// Gets or sets the error code produced when timeout failure is raised.
    /// </summary>
    public string ErrorCode { get; set; }
}
