using Krackend.Sagas.Orchestrations.WebUI.Shell;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design;

/// <summary>
/// Represents configuration options for the design WebUI module.
/// </summary>
public sealed class OrchestratorDesignWebUIOptions
{
    /// <summary>
    /// Gets or sets the route prefix used to expose the module.
    /// </summary>
    public string RoutePrefix { get; set; } = "admin/design";

    /// <summary>
    /// Gets or sets the default schema registry provider key used when creating schema bindings from the designer UI.
    /// </summary>
    public string DefaultSchemaRegistryProviderKey { get; set; } = "knowl";

    /// <summary>
    /// Gets the visual theme used by the design Web UI and embedded designers.
    /// </summary>
    public OrchestratorWebUIThemeOptions Theme { get; } = new();
}
