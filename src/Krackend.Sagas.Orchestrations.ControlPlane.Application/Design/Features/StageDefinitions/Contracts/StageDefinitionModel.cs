using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents interaction data for stage definition.
/// </summary>
public sealed class StageDefinitionModel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the orchestration version id.
    /// </summary>
    public string OrchestrationVersionId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// Gets or sets the execution condition.
    /// </summary>
    public ExecutionCondition ExecutionCondition { get; set; }
    /// <summary>
    /// Gets or sets whether execution condition is enabled.
    /// </summary>
    public bool HasExecutionCondition { get; set; }
}

