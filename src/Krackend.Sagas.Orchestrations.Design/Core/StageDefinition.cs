namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a stage in the orchestration roadmap and its execution defaults.
/// </summary>
public sealed class StageDefinition
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets orchestration version id.
    /// </summary>
    public Id OrchestrationVersionId { get; set; }

    /// <summary>
    /// Gets or sets key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets execution condition.
    /// </summary>
    public ExecutionCondition ExecutionCondition { get; set; }

    /// <summary>
    /// Gets or sets task definitions.
    /// </summary>
    public List<TaskDefinition> TaskDefinitions { get; set; } = new();

    /// <summary>
    /// Gets or sets parallel groups.
    /// </summary>
    public List<ParallelGroupDefinition> ParallelGroups { get; set; } = new();

    /// <summary>
    /// Gets or sets branch rules.
    /// </summary>
    public List<BranchRuleDefinition> BranchRules { get; set; } = new();

}
