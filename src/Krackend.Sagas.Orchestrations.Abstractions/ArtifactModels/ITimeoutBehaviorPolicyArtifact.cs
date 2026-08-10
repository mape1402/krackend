namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable timeout-behavior policy contract.
/// </summary>
public interface ITimeoutBehaviorPolicyArtifact
{
    /// <summary>
    /// Gets timeout behavior.
    /// </summary>
    TimeoutBehavior Behavior { get; }
}
