namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>
/// Configures one color palette used by Krackend orchestration Web UI modules.
/// </summary>
public sealed class OrchestratorWebUIThemePaletteOptions
{
    /// <summary>
    /// Gets or sets the primary accent color.
    /// </summary>
    public string PrimaryColor { get; set; } = "#4856d7";

    /// <summary>
    /// Gets or sets the primary accent hover color.
    /// </summary>
    public string PrimaryHoverColor { get; set; } = "#3948c7";

    /// <summary>
    /// Gets or sets the sidebar background color.
    /// </summary>
    public string SidebarBackgroundColor { get; set; } = "#1a1a2e";

    /// <summary>
    /// Gets or sets the sidebar brand background color.
    /// </summary>
    public string SidebarBrandBackgroundColor { get; set; } = "#141428";

    /// <summary>
    /// Gets or sets the sidebar text color.
    /// </summary>
    public string SidebarTextColor { get; set; } = "#d0d3e8";

    /// <summary>
    /// Gets or sets the sidebar muted text color.
    /// </summary>
    public string SidebarMutedTextColor { get; set; } = "#9094b3";

    /// <summary>
    /// Gets or sets the main content background color.
    /// </summary>
    public string ContentBackgroundColor { get; set; } = "#f3f4fa";

    /// <summary>
    /// Gets or sets the surface color used by top bars, cards, dialogs, and forms.
    /// </summary>
    public string SurfaceColor { get; set; } = "#ffffff";

    /// <summary>
    /// Gets or sets the main text color.
    /// </summary>
    public string TextColor { get; set; } = "#171a2f";

    /// <summary>
    /// Gets or sets the muted text color.
    /// </summary>
    public string MutedTextColor { get; set; } = "#5f6687";

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public string BorderColor { get; set; } = "#d9deee";

    /// <summary>
    /// Gets or sets the subtle background color.
    /// </summary>
    public string SubtleBackgroundColor { get; set; } = "#f8f9ff";

    /// <summary>
    /// Gets or sets the background color used by code blocks.
    /// </summary>
    public string CodeBackgroundColor { get; set; } = "#f7f8fc";

    /// <summary>
    /// Gets or sets the text color used by code blocks.
    /// </summary>
    public string CodeTextColor { get; set; } = "#171a2f";

    /// <summary>
    /// Gets or sets the color used by shadows. Alpha hex values are supported.
    /// </summary>
    public string ShadowColor { get; set; } = "#11182714";

    /// <summary>
    /// Copies palette values from another palette.
    /// </summary>
    /// <param name="source">Source palette.</param>
    public void ApplyFrom(OrchestratorWebUIThemePaletteOptions source)
    {
        if (source is null)
        {
            return;
        }

        PrimaryColor = source.PrimaryColor;
        PrimaryHoverColor = source.PrimaryHoverColor;
        SidebarBackgroundColor = source.SidebarBackgroundColor;
        SidebarBrandBackgroundColor = source.SidebarBrandBackgroundColor;
        SidebarTextColor = source.SidebarTextColor;
        SidebarMutedTextColor = source.SidebarMutedTextColor;
        ContentBackgroundColor = source.ContentBackgroundColor;
        SurfaceColor = source.SurfaceColor;
        TextColor = source.TextColor;
        MutedTextColor = source.MutedTextColor;
        BorderColor = source.BorderColor;
        SubtleBackgroundColor = source.SubtleBackgroundColor;
        CodeBackgroundColor = source.CodeBackgroundColor;
        CodeTextColor = source.CodeTextColor;
        ShadowColor = source.ShadowColor;
    }
}
