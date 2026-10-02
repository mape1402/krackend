namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

/// <summary>
/// Represents the orchestration scope used to open a ButterMorph mapping designer.
/// </summary>
public sealed record OrchestrationButterMorphDesignerContext
{
    /// <summary>
    /// Gets the orchestration version identifier.
    /// </summary>
    public required string OrchestrationVersionId { get; init; }

    /// <summary>
    /// Gets the task definition identifier.
    /// </summary>
    public string TaskDefinitionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the stage definition identifier.
    /// </summary>
    public string StageDefinitionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the trigger binding identifier.
    /// </summary>
    public string TriggerBindingId { get; init; } = string.Empty;
}
