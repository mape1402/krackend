namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Artifacts;

/// <summary>
/// Displays one immutable deployed artifact application in a runtime timeline.
/// </summary>
public sealed class ArtifactDeploymentHistoryRowViewModel
{
    public string ArtifactType { get; set; } = string.Empty;
    public string ReleaseId { get; set; } = string.Empty;
    public string ReleaseStrategy { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public string RuntimeStatus { get; set; } = string.Empty;
    public DateTime AppliedOnUtc { get; set; }
}
