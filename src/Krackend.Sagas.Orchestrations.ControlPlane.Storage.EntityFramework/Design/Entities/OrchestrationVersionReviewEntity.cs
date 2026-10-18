namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Durable persistence representation of an orchestration version review.
/// </summary>
public sealed class OrchestrationVersionReviewEntity
{
    /// <summary>Gets or sets the review identifier.</summary>
    public Id Id { get; set; }

    /// <summary>Gets or sets the reviewed version identifier.</summary>
    public Id OrchestrationVersionId { get; set; }

    /// <summary>Gets or sets the requested operation.</summary>
    public OrchestrationVersionReviewOperation Operation { get; set; }

    /// <summary>Gets or sets the state restored after a rejected or aborted review.</summary>
    public OrchestrationVersionStatus FromStatus { get; set; }

    /// <summary>Gets or sets the state while review is pending.</summary>
    public OrchestrationVersionStatus PendingStatus { get; set; }

    /// <summary>Gets or sets the state applied after an approved review.</summary>
    public OrchestrationVersionStatus TargetStatus { get; set; }

    /// <summary>Gets or sets the expected immutable design checksum.</summary>
    public string ExpectedChecksum { get; set; } = string.Empty;

    /// <summary>Gets or sets the approval status.</summary>
    public OrchestrationVersionReviewApprovalStatus ApprovalStatus { get; set; }

    /// <summary>Gets or sets the application status.</summary>
    public OrchestrationVersionReviewApplicationStatus ApplicationStatus { get; set; }

    /// <summary>Gets or sets the actor who requested review.</summary>
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the request timestamp.</summary>
    public DateTime RequestedOnUtc { get; set; }

    /// <summary>Gets or sets the actor deciding review.</summary>
    public string DecidedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the decision timestamp.</summary>
    public DateTime? DecidedOnUtc { get; set; }

    /// <summary>Gets or sets the optional rejection comment.</summary>
    public string RejectionComment { get; set; } = string.Empty;

    /// <summary>Gets or sets the actor aborting the review.</summary>
    public string AbortedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the abort timestamp.</summary>
    public DateTime? AbortedOnUtc { get; set; }

    /// <summary>Gets or sets the optional abort reason.</summary>
    public string AbortReason { get; set; } = string.Empty;

    /// <summary>Gets or sets the actor applying the review.</summary>
    public string AppliedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the application timestamp.</summary>
    public DateTime? AppliedOnUtc { get; set; }

    /// <summary>Gets or sets the created artifact id.</summary>
    public string DeployedArtifactId { get; set; } = string.Empty;

    /// <summary>Gets or sets a unique non-terminal review slot.</summary>
    public string PendingSlot { get; set; } = string.Empty;

    /// <summary>Gets or sets the optimistic concurrency token for this review.</summary>
    public string StateToken { get; set; } = string.Empty;
}
