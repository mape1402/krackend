using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;

/// <summary>
/// Represents BranchRuleDefinitionEntity.
/// </summary>
public sealed class BranchRuleDefinitionEntity
{
    /// <summary>
    /// Gets or sets Id.
    /// </summary>
    public Id Id { get; set; }
    /// <summary>
    /// Gets or sets StageDefinitionId.
    /// </summary>
    public Id StageDefinitionId { get; set; }
    /// <summary>
    /// Gets or sets FromType.
    /// </summary>
    public ElementType FromType { get; set; }
    /// <summary>
    /// Gets or sets FromId.
    /// </summary>
    public Id FromId { get; set; }
    /// <summary>
    /// Gets or sets NavigateToType.
    /// </summary>
    public ElementType NavigateToType { get; set; }
    /// <summary>
    /// Gets or sets NavigateToId.
    /// </summary>
    public Id NavigateToId { get; set; }
    /// <summary>
    /// Gets or sets Condition.
    /// </summary>
    public ExecutionConditionJsonModel Condition { get; set; } = new();
}
