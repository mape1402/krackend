namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable fail-on-timeout policy.
/// </summary>
public sealed record FailTimeoutBehaviorPolicyArtifact(string ErrorCode) : ITimeoutBehaviorPolicyArtifact
{
    /// <summary>
    /// Gets fail behavior.
    /// </summary>
    public TimeoutBehavior Behavior => TimeoutBehavior.Fail;
}
