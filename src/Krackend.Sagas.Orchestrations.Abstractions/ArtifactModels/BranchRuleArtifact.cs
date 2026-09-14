namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable branch-rule contract.
/// </summary>
public sealed record BranchRuleArtifact(
    Id Id,
    ElementType FromType,
    Id FromId,
    ExecutionConditionArtifact Condition,
    ElementType NavigateToType,
    Id NavigateToId);
