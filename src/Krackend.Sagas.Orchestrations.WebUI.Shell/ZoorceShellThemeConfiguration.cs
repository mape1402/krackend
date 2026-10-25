using Microsoft.Extensions.Options;
using Zoorce.Web.UI.Layout;
using Zoorce.Web.UI.Theming;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>Adapts the host-owned Krackend theme to the Zoorce shell.</summary>
public sealed class ZoorceShellThemeConfiguration : IConfigureOptions<ZoorceShellOptions>
{
    private readonly IOptions<OrchestratorWebUIThemeOptions> _theme;

    public ZoorceShellThemeConfiguration(IOptions<OrchestratorWebUIThemeOptions> theme)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
    }

    public void Configure(ZoorceShellOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var theme = _theme.Value;
        options.ApplicationName = theme.Title;
        options.BrandMark = "K";
        options.DefaultTheme = theme.Mode == OrchestratorWebUIThemeMode.Dark
            ? ZoorceThemeMode.Dark : ZoorceThemeMode.Light;
        ApplyPalette(theme.Light, options.Light);
        ApplyPalette(theme.Dark, options.Dark);
    }

    private static void ApplyPalette(OrchestratorWebUIThemePaletteOptions source, ZoorceThemePalette target)
    {
        target.Primary = source.PrimaryColor;
        target.PrimaryHover = source.PrimaryHoverColor;
        target.Sidebar = source.SidebarBackgroundColor;
        target.SidebarText = source.SidebarTextColor;
        target.SidebarMuted = source.SidebarMutedTextColor;
        target.Canvas = source.ContentBackgroundColor;
        target.Surface = source.SurfaceColor;
        target.Text = source.TextColor;
        target.Muted = source.MutedTextColor;
        target.Border = source.BorderColor;
        target.Subtle = source.SubtleBackgroundColor;
        target.CodeCanvas = source.CodeBackgroundColor;
        target.CodeText = source.CodeTextColor;
        target.Shadow = source.ShadowColor;
    }
}
