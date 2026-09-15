namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI;

/// <summary>
/// Defines route options for the orchestration control-plane Web UI.
/// </summary>
public sealed class OrchestratorControlPlaneWebUIOptions
{
    /// <summary>
    /// Gets or sets the route prefix used by the design area.
    /// </summary>
    public string DesignRoutePrefix { get; set; } = "admin";

    /// <summary>
    /// Gets or sets the default schema registry provider key used when creating schema bindings from the designer UI.
    /// </summary>
    public string DefaultSchemaRegistryProviderKey { get; set; } = "knowl";

    /// <summary>
    /// Gets or sets the route prefix used by the distribution area.
    /// </summary>
    public string DistributionRoutePrefix { get; set; } = "admin/orchestrator-distribution";

    /// <summary>
    /// Gets or sets the route prefix used by the security area.
    /// </summary>
    public string SecurityRoutePrefix { get; set; } = "admin/orchestrator-security";
}
