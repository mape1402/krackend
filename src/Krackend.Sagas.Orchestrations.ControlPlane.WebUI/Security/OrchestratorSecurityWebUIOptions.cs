namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Security;

/// <summary>
/// Represents configuration options for Security WebUI module.
/// </summary>
public sealed class OrchestratorSecurityWebUIOptions
{
    /// <summary>
    /// Gets or sets route prefix.
    /// </summary>
    public string RoutePrefix { get; set; } = "orchestrator-security";
}
