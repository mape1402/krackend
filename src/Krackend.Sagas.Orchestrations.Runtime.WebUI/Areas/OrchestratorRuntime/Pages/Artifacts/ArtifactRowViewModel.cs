namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Artifacts;

/// <summary>
/// Displays one runtime orchestration artifact lifecycle row.
/// </summary>
public sealed class ArtifactRowViewModel
{
    /// <summary>
    /// Gets or sets the runtime artifact id.
    /// </summary>
    public string ArtifactId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the orchestration definition key.
    /// </summary>
    public string OrchestrationDefinitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the orchestration version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this artifact is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the ingress generation.
    /// </summary>
    public long IngressGeneration { get; set; }

    /// <summary>
    /// Gets or sets the artifact checksum.
    /// </summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the artifact was deployed.
    /// </summary>
    public DateTime DeployedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets when the artifact was activated.
    /// </summary>
    public DateTime? ActivatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets when ingress projection started.
    /// </summary>
    public DateTime? ProjectionStartedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets when ingress projection completed.
    /// </summary>
    public DateTime? ProjectionCompletedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets when ingress projection failed.
    /// </summary>
    public DateTime? ProjectionFailedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the last ingress projection error.
    /// </summary>
    public string ProjectionError { get; set; } = string.Empty;
}
