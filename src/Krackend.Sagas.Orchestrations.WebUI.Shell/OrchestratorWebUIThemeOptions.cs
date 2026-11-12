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
        Light.SurfaceColor = "#ffffff";
        Light.CodeBackgroundColor = "#111b2a";
        Light.CodeTextColor = "#e0ebff";
        Light.ShadowColor = "#1a315424";
        Light.OverlayColor = "#101b2e8f";
        Dark.CodeBackgroundColor = "#0c121e";
        Dark.CodeTextColor = "#e1ebff";
        Dark.ShadowColor = "#0000007a";
        Dark.SuccessSoftColor = "#123d2b";
        Dark.WarningColor = "#f2bd58";
        Dark.WarningSoftColor = "#3a2c12";
        Dark.DangerForegroundColor = "#29070d";
        Dark.DangerSoftColor = "#491720";
        Dark.InfoSoftColor = "#1c2b52";
        Dark.DataPrimaryColor = "#7fb0ff";
        Dark.DataSecondaryColor = "#b494ff";
        Dark.DataTertiaryColor = "#f08dde";
        Dark.DataQuaternaryColor = "#ffc86a";
        Dark.DataQuinaryColor = "#56d5c7";
        Dark.OverlayColor = "#070b129e";
        Light.PrimaryColor = "#087a57"; Light.PrimaryHoverColor = "#056446";
        Light.PrimarySoftColor = "#d9f2e7"; Light.SidebarBackgroundColor = "#eef5f1";
        Light.SidebarBrandBackgroundColor = "#e4f0ea"; Light.SidebarTextColor = "#17362a";
        Light.SidebarMutedTextColor = "#587267"; Light.ContentBackgroundColor = "#f6f8f7";
        Light.TextColor = "#15251f"; Light.MutedTextColor = "#52675e"; Light.BorderColor = "#d6e2dc";
        Light.SubtleBackgroundColor = "#edf3f0"; Light.DataPrimaryColor = "#2477c9";
        Light.DataSecondaryColor = "#7156c5"; Light.DataTertiaryColor = "#c13e9a";
        Light.DataQuaternaryColor = "#c57908"; Light.DataQuinaryColor = "#0a8075";
        Dark.PrimaryColor = "#20e7a0"; Dark.PrimaryHoverColor = "#53f3be"; Dark.PrimarySoftColor = "#123f31";
        Dark.SidebarBackgroundColor = "#07120f"; Dark.SidebarBrandBackgroundColor = "#06100d";
        Dark.SidebarTextColor = "#eefff8"; Dark.SidebarMutedTextColor = "#83a89a";
        Dark.ContentBackgroundColor = "#050b0a"; Dark.SurfaceColor = "#0b1613"; Dark.ElevatedColor = "#10201b";
        Dark.TextColor = "#edfff7"; Dark.MutedTextColor = "#93aa9f"; Dark.BorderColor = "#1d3930";
        Dark.SubtleBackgroundColor = "#132720"; Dark.SuccessColor = "#20e7a0";
        Dark.InfoColor = "#8eb0ff"; Dark.DangerColor = "#ff7586";
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
    /// Gets or sets the optional context label rendered above a page title in the top bar.
    /// Hosts can use this for a product area such as <c>Control plane</c>; when omitted,
    /// the shell intentionally renders no secondary product label.
    /// </summary>
    public string TopBarContext { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compact text mark rendered by the Zoorce shell when no image mark is used.
    /// </summary>
    public string BrandMark { get; set; } = "K";

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
        TopBarContext = source.TopBarContext;
        BrandMark = source.BrandMark;
        Mode = source.Mode;
        IconCssClass = source.IconCssClass;
        IconImageUrl = source.IconImageUrl;
        Light.ApplyFrom(source.Light);
        Dark.ApplyFrom(source.Dark);
    }
}
