namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Captures an auditable request to apply a guarded orchestration lifecycle transition.
/// </summary>
public sealed class OrchestrationVersionReview
{
    /// <summary>
    /// Gets or sets the review identifier.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets the reviewed orchestration version identifier.
    /// </summary>
    public Id OrchestrationVersionId { get; set; }

    /// <summary>
    /// Gets or sets the requested lifecycle operation.
    /// </summary>
    public OrchestrationVersionReviewOperation Operation { get; set; }

    /// <summary>
    /// Gets or sets the state restored when the review is rejected or aborted.
    /// </summary>
    public OrchestrationVersionStatus FromStatus { get; set; }

    /// <summary>
    /// Gets or sets the version state while the review is pending.
    /// </summary>
    public OrchestrationVersionStatus PendingStatus { get; set; }

    /// <summary>
    /// Gets or sets the state applied after the review is approved and applied.
    /// </summary>
    public OrchestrationVersionStatus TargetStatus { get; set; }

    /// <summary>
    /// Gets or sets the design checksum captured at request time.
    /// </summary>
    public required string ExpectedChecksum { get; set; }

    /// <summary>
    /// Gets or sets the approval decision state.
    /// </summary>
    public OrchestrationVersionReviewApprovalStatus ApprovalStatus { get; set; } = OrchestrationVersionReviewApprovalStatus.Pending;

    /// <summary>
    /// Gets or sets the lifecycle application state.
    /// </summary>
    public OrchestrationVersionReviewApplicationStatus ApplicationStatus { get; set; } = OrchestrationVersionReviewApplicationStatus.Pending;

    /// <summary>
    /// Gets or sets the actor requesting the transition.
    /// </summary>
    public required string RequestedBy { get; set; }

    /// <summary>
    /// Gets or sets when the transition was requested.
    /// </summary>
    public DateTime RequestedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the actor deciding the review.
    /// </summary>
    public string DecidedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the review was decided.
    /// </summary>
    public DateTime? DecidedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the optional rejection comment.
    /// </summary>
    public string RejectionComment { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actor aborting an approved or pending review.
    /// </summary>
    public string AbortedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the review was aborted.
    /// </summary>
    public DateTime? AbortedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the optional abort reason.
    /// </summary>
    public string AbortReason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actor applying the approved review.
    /// </summary>
    public string AppliedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the transition was applied.
    /// </summary>
    public DateTime? AppliedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the generated deployed artifact identifier after application.
    /// </summary>
    public string DeployedArtifactId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optimistic concurrency token for lifecycle decisions and application.
    /// </summary>
    public string StateToken { get; set; } = Guid.NewGuid().ToString("N");
}

/// <summary>
/// Defines the lifecycle request represented by an orchestration version review.
/// </summary>
public enum OrchestrationVersionReviewOperation
{
    /// <summary>Deploy a draft version.</summary>
    Deploy,
    /// <summary>Deprecate a deployed version.</summary>
    Deprecate,
    /// <summary>Archive a deprecated version.</summary>
    Archive,
}

/// <summary>
/// Defines the approval state of a review.
/// </summary>
public enum OrchestrationVersionReviewApprovalStatus
{
    /// <summary>Awaiting a decision.</summary>
    Pending,
    /// <summary>Approved and eligible for application.</summary>
    Approved,
    /// <summary>Rejected and restored to its original version state.</summary>
    Rejected,
}

/// <summary>
/// Defines the application state of a review decision.
/// </summary>
public enum OrchestrationVersionReviewApplicationStatus
{
    /// <summary>Not yet applied.</summary>
    Pending,
    /// <summary>Transition was applied and a deployed artifact was generated.</summary>
    Applied,
    /// <summary>Transition will not be applied.</summary>
    Aborted,
}
