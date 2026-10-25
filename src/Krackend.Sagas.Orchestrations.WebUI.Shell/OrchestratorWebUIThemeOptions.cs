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
        Light.PrimaryColor = "#0087d1";
        Light.PrimaryHoverColor = "#006aab";
        Light.SidebarBackgroundColor = "#f0f0f8";
        Light.SidebarBrandBackgroundColor = "#ededf6";
        Light.SidebarTextColor = "#292b3c";
        Light.SidebarMutedTextColor = "#666a7e";
        Light.ContentBackgroundColor = "#fafafd";
        Light.SurfaceColor = "#ffffff";
        Light.TextColor = "#22232e";
        Light.MutedTextColor = "#5e6171";
        Light.BorderColor = "#dedeea";
        Light.SubtleBackgroundColor = "#f1f1f8";
        Dark.PrimaryColor = "#0087d1";
        Dark.PrimaryHoverColor = "#47b3ea";
        Dark.SidebarBackgroundColor = "#13131b";
        Dark.SidebarBrandBackgroundColor = "#101017";
        Dark.SidebarTextColor = "#f1f0f8";
        Dark.SidebarMutedTextColor = "#aaa9bb";
        Dark.ContentBackgroundColor = "#0d0d13";
        Dark.SurfaceColor = "#171720";
        Dark.TextColor = "#f2f1f8";
        Dark.MutedTextColor = "#b9b8c8";
        Dark.BorderColor = "#373744";
        Dark.SubtleBackgroundColor = "#262632";
        Dark.CodeBackgroundColor = "#111118";
        Dark.CodeTextColor = "#e8e7f2";
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
