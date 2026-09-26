namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable task contract inside a stage artifact.
/// </summary>
public sealed record TaskArtifact(
    Id Id,
    string Key,
    string Name,
    int Order,
    string Notes,
    TaskKind Kind,
    TaskExecutionMode ExecutionMode,
    Id? ParallelGroupId,
    ExecutionConditionArtifact ExecutionCondition,
    TransformationArtifact Transformation,
    ITaskConfigurationArtifact Configuration,
    RetryPolicyArtifact RetryPolicy,
    TimeoutPolicyArtifact TimeoutPolicy,
    OnErrorPolicy OnErrorPolicy,
    CompensationArtifact Compensation,
    TaskDispatchType DispatchType,
    bool IsEnabled);
