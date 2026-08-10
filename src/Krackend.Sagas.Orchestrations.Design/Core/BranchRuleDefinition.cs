namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a branch rule that controls navigation between orchestration elements.
/// </summary>
public sealed class BranchRuleDefinition
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets owner type.
    /// </summary>
    public required ElementType FromType { get; set; }

    /// <summary>
    /// Gets or sets owner id.
    /// </summary>
    public Id FromId { get; set; }

    /// <summary>
    /// Gets or sets condition.
    /// </summary>
    public required ExecutionCondition Condition { get; set; }

    /// <summary>
    /// Gets or sets target type.
    /// </summary>
    public required ElementType NavigateToType { get; set; }

    /// <summary>
    /// Gets or sets target key.
    /// </summary>
    public Id NavigateToId { get; set; }
}
