namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design;

/// <summary>
/// Represents configuration options for the design WebUI module.
/// </summary>
public sealed class OrchestratorDesignWebUIOptions
{
    /// <summary>
    /// Gets or sets the route prefix used to expose the module.
    /// </summary>
    public string RoutePrefix { get; set; } = "orchestrator-design";

    /// <summary>
    /// Gets or sets the default schema registry provider key used when creating schema bindings from the designer UI.
    /// </summary>
    public string DefaultSchemaRegistryProviderKey { get; set; } = "knowl";
}
