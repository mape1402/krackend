namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable stage contract inside an orchestration artifact.
/// </summary>
public sealed record StageArtifact(
    Id Id,
    string Key,
    string Name,
    int Order,
    ExecutionConditionArtifact ExecutionCondition,
    IReadOnlyList<TaskArtifact> TaskDefinitions,
    IReadOnlyList<ParallelGroupArtifact> ParallelGroups,
    IReadOnlyList<BranchRuleArtifact> BranchRules,
    string Description = "");
