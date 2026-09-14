namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable compensation contract for a task.
/// </summary>
public sealed record CompensationArtifact(
    TaskKind CompensationTaskKind,
    TransformationArtifact Transformation,
    ExecutionConditionArtifact ExecutionCondition,
    ITaskConfigurationArtifact Configuration,
    RetryPolicyArtifact RetryPolicy,
    TimeoutPolicyArtifact TimeoutPolicy,
    TaskDispatchType DispatchType);
