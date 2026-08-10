using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents interaction data for branch rule definition.
/// </summary>
public sealed class BranchRuleDefinitionModel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the from type.
    /// </summary>
    public ElementType FromType { get; set; }
    /// <summary>
    /// Gets or sets the from id.
    /// </summary>
    public string FromId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the condition.
    /// </summary>
    public ExecutionCondition Condition { get; set; } = null!;
    /// <summary>
    /// Gets or sets the navigate to type.
    /// </summary>
    public ElementType NavigateToType { get; set; }
    /// <summary>
    /// Gets or sets the navigate to id.
    /// </summary>
    public string NavigateToId { get; set; } = string.Empty;
}

