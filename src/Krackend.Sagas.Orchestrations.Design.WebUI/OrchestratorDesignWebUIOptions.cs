namespace Krackend.Sagas.Orchestrations.Design.WebUI;

/// <summary>
/// Represents configuration options for the design WebUI module.
/// </summary>
public sealed class OrchestratorDesignWebUIOptions
{
    /// <summary>
    /// Gets or sets the route prefix used to expose the module.
    /// </summary>
    public string RoutePrefix { get; set; } = "orchestrator-design";
}
