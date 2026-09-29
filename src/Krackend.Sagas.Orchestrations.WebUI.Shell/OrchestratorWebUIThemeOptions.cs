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
        Dark.PrimaryColor = "#8b8ff5";
        Dark.PrimaryHoverColor = "#a5a8ff";
        Dark.SidebarBackgroundColor = "#111226";
        Dark.SidebarBrandBackgroundColor = "#0b0c1d";
        Dark.SidebarTextColor = "#f0f2ff";
        Dark.SidebarMutedTextColor = "#a4abc9";
        Dark.ContentBackgroundColor = "#101322";
        Dark.SurfaceColor = "#171a2f";
        Dark.TextColor = "#edf0ff";
        Dark.MutedTextColor = "#aeb5d4";
        Dark.BorderColor = "#2e365a";
        Dark.SubtleBackgroundColor = "#202640";
        Dark.CodeBackgroundColor = "#0f1324";
        Dark.CodeTextColor = "#dbe4ff";
        Dark.ShadowColor = "#00000066";
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
