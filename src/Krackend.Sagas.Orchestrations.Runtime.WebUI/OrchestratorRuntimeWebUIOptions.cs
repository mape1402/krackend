using Krackend.Sagas.Orchestrations.WebUI.Shell;

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

    /// <summary>Optional authorization policy for operator actions in the runtime UI.</summary>
    public string ManagePolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets the visual theme and branding options used by the Runtime Web UI.
    /// </summary>
    public OrchestratorWebUIThemeOptions Theme { get; } = new();

    /// <summary>
    /// Copies route and theme values from another runtime Web UI configuration.
    /// </summary>
    /// <param name="source">Source runtime Web UI configuration.</param>
    public void ApplyFrom(OrchestratorRuntimeWebUIOptions source)
    {
        if (source is null)
        {
            return;
        }

        RoutePrefix = source.RoutePrefix;
        ManagePolicy = source.ManagePolicy;
        Theme.ApplyFrom(source.Theme);
    }
}
