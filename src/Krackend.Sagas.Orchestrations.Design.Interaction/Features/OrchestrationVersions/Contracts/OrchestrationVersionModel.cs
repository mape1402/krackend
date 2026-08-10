using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents interaction data for orchestration version.
/// </summary>
public sealed class OrchestrationVersionModel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the orchestration definition id.
    /// </summary>
    public string OrchestrationDefinitionId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public string Version { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public OrchestrationVersionStatus Status { get; set; }
    /// <summary>
    /// Gets or sets the version label.
    /// </summary>
    public string VersionLabel { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the checksum.
    /// </summary>
    public string Checksum { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the trigger bindings count.
    /// </summary>
    public int TriggerBindingsCount { get; set; }
    /// <summary>
    /// Gets or sets the stage definitions count.
    /// </summary>
    public int StageDefinitionsCount { get; set; }
    /// <summary>
    /// Gets or sets the variable definitions count.
    /// </summary>
    public int VariableDefinitionsCount { get; set; }
    /// <summary>
    /// Gets or sets the created on utc.
    /// </summary>
    public string CreatedOnUtc { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the created by.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the approved on utc.
    /// </summary>
    public string ApprovedOnUtc { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the approved by.
    /// </summary>
    public string ApprovedBy { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the updated on utc.
    /// </summary>
    public string UpdatedOnUtc { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the updated by.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}


