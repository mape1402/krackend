namespace Krackend.Sagas.Orchestrations.Runtime.WebUI;

/// <summary>
/// Configures route options for the runtime diagnostics UI module.
/// </summary>
public sealed class OrchestratorRuntimeWebUIOptions
{
    /// <summary>
    /// Gets or sets the route prefix used by runtime diagnostics pages and live endpoints.
    /// </summary>
    public string RoutePrefix { get; set; } = "runtime";

}
