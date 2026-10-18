namespace Krackend.Sagas.Orchestrations.ControlPlane.Api;

using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>Represents a request to begin a guarded orchestration lifecycle transition.</summary>
public sealed class LifecycleReviewRequest : ActorRequest
{
    public OrchestrationVersionReviewOperation Operation { get; set; }
}

/// <summary>Represents an optional human comment attached to a review decision.</summary>
public sealed class LifecycleReviewDecisionRequest : ActorRequest
{
    public string Comment { get; set; } = string.Empty;
}

/// <summary>Contains the deployment metadata required only when applying a deployment review.</summary>
public sealed class LifecycleReviewApplyRequest : ActorRequest
{
    public string Version { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
