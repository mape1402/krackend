namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>Represents an auditable lifecycle review in the application boundary.</summary>
public sealed class OrchestrationVersionReviewModel
{
    public string Id { get; set; } = string.Empty;
    public string OrchestrationVersionId { get; set; } = string.Empty;
    public OrchestrationVersionReviewOperation Operation { get; set; }
    public OrchestrationVersionStatus FromStatus { get; set; }
    public OrchestrationVersionStatus PendingStatus { get; set; }
    public OrchestrationVersionStatus TargetStatus { get; set; }
    public OrchestrationVersionReviewApprovalStatus ApprovalStatus { get; set; }
    public OrchestrationVersionReviewApplicationStatus ApplicationStatus { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedOnUtc { get; set; }
    public string DecidedBy { get; set; } = string.Empty;
    public DateTime? DecidedOnUtc { get; set; }
    public string RejectionComment { get; set; } = string.Empty;
    public string AbortedBy { get; set; } = string.Empty;
    public DateTime? AbortedOnUtc { get; set; }
    public string AbortReason { get; set; } = string.Empty;
    public string AppliedBy { get; set; } = string.Empty;
    public DateTime? AppliedOnUtc { get; set; }
    public string DeployedArtifactId { get; set; } = string.Empty;
}
