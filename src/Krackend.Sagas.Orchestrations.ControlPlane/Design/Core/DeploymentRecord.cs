namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable snapshot of a deployment attempt for an orchestration version.
/// </summary>
public sealed class DeploymentRecord
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
    /// Gets or sets mode.
    /// </summary>
    public DeploymentMode Mode { get; set; }

    /// <summary>
    /// Gets or sets status.
    /// </summary>
    public DeploymentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets artifact checksum.
    /// </summary>
    public required Checksum ArtifactChecksum { get; set; }

    /// <summary>
    /// Gets or sets artifact version.
    /// </summary>
    public required SemanticVersion ArtifactVersion { get; set; }

    /// <summary>
    /// Gets or sets is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets deployed on utc.
    /// </summary>
    public DateTime? DeployedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets deployed by.
    /// </summary>
    public string DeployedBy { get; set; }

    /// <summary>
    /// Gets or sets failure reason.
    /// </summary>
    public string FailureReason { get; set; }

    /// <summary>
    /// Gets or sets notes.
    /// </summary>
    public string Notes { get; set; }
}
