namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.DesignNodes;

/// <summary>
/// Displays an artifact package available for manual pull.
/// </summary>
public sealed class PendingArtifactViewModel
{
    /// <summary>
    /// Gets or sets the release target id.
    /// </summary>
    public string ReleaseTargetId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the orchestration definition key.
    /// </summary>
    public string OrchestrationDefinitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the orchestration version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact checksum.
    /// </summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the promotion correlation id.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the promotion actor.
    /// </summary>
    public string PromotedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the artifact was promoted.
    /// </summary>
    public DateTime PromotedOnUtc { get; set; }
}
