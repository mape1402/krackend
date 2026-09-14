namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.RuntimeNodes;

/// <summary>
/// Captures Runtime credentials imported into Design.
/// </summary>
public sealed class RuntimeNodeCredentialsInput
{
    /// <summary>
    /// Gets or sets the runtime node id.
    /// </summary>
    public string RuntimeNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Runtime credentials JSON.
    /// </summary>
    public string CredentialsJson { get; set; } = string.Empty;
}
