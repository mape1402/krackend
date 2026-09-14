namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable timeout-behavior policy contract.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$artifactType")]
[JsonDerivedType(typeof(FailTimeoutBehaviorPolicyArtifact), "fail")]
[JsonDerivedType(typeof(WaitTimeoutBehaviorPolicyArtifact), "wait")]
[JsonDerivedType(typeof(ReconcileTimeoutBehaviorPolicyArtifact), "reconcile")]
public interface ITimeoutBehaviorPolicyArtifact
{
    /// <summary>
    /// Gets timeout behavior.
    /// </summary>
    TimeoutBehavior Behavior { get; }
}
