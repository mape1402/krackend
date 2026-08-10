namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable timeout-policy contract.
/// </summary>
public sealed record TimeoutPolicyArtifact(
    Duration Timeout,
    TimeoutBehavior TimeoutBehavior,
    ITimeoutBehaviorPolicyArtifact TimeoutBehaviorPolicy);
