using Krackend.Sagas.Orchestrations.WebUI.Shell;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI;

/// <summary>
/// Configures route options for the runtime diagnostics UI module.
/// </summary>
public sealed class OrchestratorRuntimeWebUIOptions
{
    /// <summary>Creates the Runtime theme with the reference dashboard palette. Hosts may override every color.</summary>
    public OrchestratorRuntimeWebUIOptions()
    {
        var light = Theme.Light;
        light.PrimaryColor = "#087a57"; light.PrimaryHoverColor = "#056446";
        light.PrimarySoftColor = "#d9f2e7"; light.SidebarBackgroundColor = "#eef5f1";
        light.SidebarBrandBackgroundColor = "#e4f0ea"; light.SidebarTextColor = "#17362a";
        light.SidebarMutedTextColor = "#587267"; light.ContentBackgroundColor = "#f6f8f7";
        light.TextColor = "#15251f"; light.MutedTextColor = "#52675e"; light.BorderColor = "#d6e2dc";
        light.SubtleBackgroundColor = "#edf3f0"; light.DataPrimaryColor = "#2477c9";
        light.DataSecondaryColor = "#7156c5"; light.DataTertiaryColor = "#c13e9a";
        light.DataQuaternaryColor = "#c57908"; light.DataQuinaryColor = "#0a8075";
        var dark = Theme.Dark;
        dark.PrimaryColor = "#20e7a0"; dark.PrimaryHoverColor = "#53f3be"; dark.PrimarySoftColor = "#123f31";
        dark.SidebarBackgroundColor = "#07120f"; dark.SidebarBrandBackgroundColor = "#06100d";
        dark.SidebarTextColor = "#eefff8"; dark.SidebarMutedTextColor = "#83a89a";
        dark.ContentBackgroundColor = "#050b0a"; dark.SurfaceColor = "#0b1613"; dark.ElevatedColor = "#10201b";
        dark.TextColor = "#edfff7"; dark.MutedTextColor = "#93aa9f"; dark.BorderColor = "#1d3930";
        dark.SubtleBackgroundColor = "#132720"; dark.SuccessColor = "#20e7a0";
        dark.InfoColor = "#8eb0ff"; dark.DangerColor = "#ff7586";
    }
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
