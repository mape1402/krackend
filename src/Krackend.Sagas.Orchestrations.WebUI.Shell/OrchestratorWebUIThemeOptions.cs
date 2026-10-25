namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>
/// Configures the visual branding used by Krackend orchestration Web UI modules.
/// </summary>
public sealed class OrchestratorWebUIThemeOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratorWebUIThemeOptions"/> class.
    /// </summary>
    public OrchestratorWebUIThemeOptions()
    {
        Light.PrimaryColor = "#6d28d9";
        Light.PrimaryHoverColor = "#5b21b6";
        Light.SidebarBackgroundColor = "#f3efff";
        Light.SidebarBrandBackgroundColor = "#f3efff";
        Light.SidebarTextColor = "#2d2145";
        Light.SidebarMutedTextColor = "#74668c";
        Light.ContentBackgroundColor = "#faf8ff";
        Light.SurfaceColor = "#ffffff";
        Light.TextColor = "#211b2e";
        Light.MutedTextColor = "#665d78";
        Light.BorderColor = "#ddd6ee";
        Light.SubtleBackgroundColor = "#f1edff";
        Light.CodeBackgroundColor = "#171022";
        Light.CodeTextColor = "#ece5ff";
        Light.ShadowColor = "#2e1e5024";
        Light.OverlayColor = "#1a102b8f";
        Light.DataSecondaryColor = "#7c3aed";
        Light.DataTertiaryColor = "#c026d3";
        Light.DataQuaternaryColor = "#d97706";
        Light.DataQuinaryColor = "#0f766e";
        Dark.PrimaryColor = "#a78bfa";
        Dark.PrimaryHoverColor = "#c4b5fd";
        Dark.SidebarBackgroundColor = "#120d1b";
        Dark.SidebarBrandBackgroundColor = "#120d1b";
        Dark.SidebarTextColor = "#f7f2ff";
        Dark.SidebarMutedTextColor = "#aca0c3";
        Dark.ContentBackgroundColor = "#0d0a13";
        Dark.SurfaceColor = "#16101f";
        Dark.TextColor = "#f7f2ff";
        Dark.MutedTextColor = "#b9afc9";
        Dark.BorderColor = "#39294d";
        Dark.SubtleBackgroundColor = "#281d37";
        Dark.CodeBackgroundColor = "#110b1a";
        Dark.CodeTextColor = "#e9ddff";
        Dark.ShadowColor = "#0000007a";
        Dark.SuccessColor = "#39d98a";
        Dark.SuccessSoftColor = "#123d2b";
        Dark.WarningColor = "#f2bd58";
        Dark.WarningSoftColor = "#3a2c12";
        Dark.DangerColor = "#ff8291";
        Dark.DangerForegroundColor = "#29070d";
        Dark.DangerSoftColor = "#491720";
        Dark.InfoColor = "#94b6ff";
        Dark.InfoSoftColor = "#1c2b52";
        Dark.DataPrimaryColor = "#7fb0ff";
        Dark.DataSecondaryColor = "#b494ff";
        Dark.DataTertiaryColor = "#f08dde";
        Dark.DataQuaternaryColor = "#ffc86a";
        Dark.DataQuinaryColor = "#56d5c7";
        Dark.OverlayColor = "#07040b9e";
    }

    /// <summary>
    /// Gets or sets the product title rendered in the sidebar brand and browser title.
    /// </summary>
    public string Title { get; set; } = "Orchestrator";

    /// <summary>
    /// Gets or sets the optional subtitle rendered below the sidebar brand title.
    /// </summary>
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the configured color mode.
    /// </summary>
    public OrchestratorWebUIThemeMode Mode { get; set; } = OrchestratorWebUIThemeMode.Light;

    /// <summary>
    /// Gets or sets the Bootstrap Icons class used for the sidebar brand icon when <see cref="IconImageUrl"/> is not set.
    /// </summary>
    public string IconCssClass { get; set; } = "bi-diagram-3-fill";

    /// <summary>
    /// Gets or sets an optional image URL used as the sidebar brand icon.
    /// </summary>
    public string IconImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets the light color palette configured by the host.
    /// </summary>
    public OrchestratorWebUIThemePaletteOptions Light { get; } = new();

    /// <summary>
    /// Gets the dark color palette configured by the host.
    /// </summary>
    public OrchestratorWebUIThemePaletteOptions Dark { get; } = new();

    /// <summary>
    /// Gets or sets the primary accent color used by buttons, links, and active states.
    /// </summary>
    public string PrimaryColor
    {
        get => Light.PrimaryColor;
        set => Light.PrimaryColor = value;
    }

    /// <summary>
    /// Gets or sets the primary accent hover color.
    /// </summary>
    public string PrimaryHoverColor
    {
        get => Light.PrimaryHoverColor;
        set => Light.PrimaryHoverColor = value;
    }

    /// <summary>
    /// Gets or sets the sidebar background color.
    /// </summary>
    public string SidebarBackgroundColor
    {
        get => Light.SidebarBackgroundColor;
        set => Light.SidebarBackgroundColor = value;
    }

    /// <summary>
    /// Gets or sets the sidebar brand background color.
    /// </summary>
    public string SidebarBrandBackgroundColor
    {
        get => Light.SidebarBrandBackgroundColor;
        set => Light.SidebarBrandBackgroundColor = value;
    }

    /// <summary>
    /// Gets or sets the sidebar text color.
    /// </summary>
    public string SidebarTextColor
    {
        get => Light.SidebarTextColor;
        set => Light.SidebarTextColor = value;
    }

    /// <summary>
    /// Gets or sets the sidebar muted text color.
    /// </summary>
    public string SidebarMutedTextColor
    {
        get => Light.SidebarMutedTextColor;
        set => Light.SidebarMutedTextColor = value;
    }

    /// <summary>
    /// Gets or sets the main content background color.
    /// </summary>
    public string ContentBackgroundColor
    {
        get => Light.ContentBackgroundColor;
        set => Light.ContentBackgroundColor = value;
    }

    /// <summary>
    /// Gets or sets the surface color used by top bars, cards, dialogs, and forms.
    /// </summary>
    public string SurfaceColor
    {
        get => Light.SurfaceColor;
        set => Light.SurfaceColor = value;
    }

    /// <summary>
    /// Gets or sets the main text color.
    /// </summary>
    public string TextColor
    {
        get => Light.TextColor;
        set => Light.TextColor = value;
    }

    /// <summary>
    /// Gets the CSS color mode value used by Bootstrap and browser form controls.
    /// </summary>
    public string CssMode => Mode == OrchestratorWebUIThemeMode.Dark ? "dark" : "light";

    /// <summary>
    /// Copies theme values from another theme configuration.
    /// </summary>
    /// <param name="source">Source theme configuration.</param>
    public void ApplyFrom(OrchestratorWebUIThemeOptions source)
    {
        if (source is null)
        {
            return;
        }

        Title = source.Title;
        Subtitle = source.Subtitle;
        Mode = source.Mode;
        IconCssClass = source.IconCssClass;
        IconImageUrl = source.IconImageUrl;
        Light.ApplyFrom(source.Light);
        Dark.ApplyFrom(source.Dark);
    }
}
