namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a versioned and deployable snapshot of an orchestration definition.
/// </summary>
public sealed class OrchestrationVersion
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets orchestration definition id.
    /// </summary>
    public Id OrchestrationDefinitionId { get; set; }

    /// <summary>
    /// Gets or sets version.
    /// </summary>
    public required SemanticVersion Version { get; set; }

    /// <summary>
    /// Gets or sets status.
    /// </summary>
    public OrchestrationVersionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets version label.
    /// </summary>
    public string VersionLabel { get; set; }

    /// <summary>
    /// Gets or sets description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets checksum.
    /// </summary>
    public required Checksum Checksum { get; set; }

    /// <summary>
    /// Gets or sets trigger bindings.
    /// </summary>
    public List<TriggerBinding> TriggerBindings { get; set; } = new();

    /// <summary>
    /// Gets or sets stage definitions.
    /// </summary>
    public List<StageDefinition> StageDefinitions { get; set; } = new();

    /// <summary>
    /// Gets or sets variable definitions.
    /// </summary>
    public List<VariableDefinition> VariableDefinitions { get; set; } = new();

    /// <summary>
    /// Gets or sets notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets created on utc.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets created by.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets approved on utc.
    /// </summary>
    public DateTime? ApprovedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets approved by.
    /// </summary>
    public string ApprovedBy { get; set; }

    /// <summary>
    /// Gets or sets updated on utc.
    /// </summary>
    public DateTime? UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets updated by.
    /// </summary>
    public string UpdatedBy { get; set; }
}
