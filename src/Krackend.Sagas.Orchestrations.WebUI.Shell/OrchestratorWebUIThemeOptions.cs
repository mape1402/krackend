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
        Light.PrimaryColor = "#235bc0";
        Light.PrimaryHoverColor = "#18479d";
        Light.SidebarBackgroundColor = "#eaf1fb";
        Light.SidebarBrandBackgroundColor = "#e3edf9";
        Light.SidebarTextColor = "#152c4d";
        Light.SidebarMutedTextColor = "#536c8a";
        Light.ContentBackgroundColor = "#f7f9fc";
        Light.SurfaceColor = "#ffffff";
        Light.TextColor = "#172a45";
        Light.MutedTextColor = "#586d87";
        Light.BorderColor = "#d6e1ee";
        Light.SubtleBackgroundColor = "#eef3f9";
        Dark.PrimaryColor = "#8db8ff";
        Dark.PrimaryHoverColor = "#b0cdff";
        Dark.SidebarBackgroundColor = "#101b2c";
        Dark.SidebarBrandBackgroundColor = "#0d1726";
        Dark.SidebarTextColor = "#ecf4ff";
        Dark.SidebarMutedTextColor = "#a4b5ce";
        Dark.ContentBackgroundColor = "#0c1421";
        Dark.SurfaceColor = "#152338";
        Dark.TextColor = "#edf4ff";
        Dark.MutedTextColor = "#aabbd1";
        Dark.BorderColor = "#30445f";
        Dark.SubtleBackgroundColor = "#1d304a";
        Dark.CodeBackgroundColor = "#101b2b";
        Dark.CodeTextColor = "#dceaff";
        Dark.ShadowColor = "#00000070";
        Dark.SuccessColor = "#55d99c";
        Dark.SuccessSoftColor = "#173d30";
        Dark.WarningColor = "#f2c36b";
        Dark.WarningSoftColor = "#443316";
        Dark.DangerColor = "#ff92a1";
        Dark.DangerForegroundColor = "#29070d";
        Dark.DangerSoftColor = "#4b2029";
        Dark.InfoColor = "#a9c7ff";
        Dark.InfoSoftColor = "#233a5e";
        Dark.DataPrimaryColor = "#80b4ff";
        Dark.DataSecondaryColor = "#bda5ff";
        Dark.DataTertiaryColor = "#f29bd7";
        Dark.DataQuaternaryColor = "#ffd27b";
        Dark.DataQuinaryColor = "#64d8c7";
        Dark.OverlayColor = "#04080e9e";
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
