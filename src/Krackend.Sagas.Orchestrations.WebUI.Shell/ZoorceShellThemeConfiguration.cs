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
        ApplyPalette(theme.Light, options.Light, false);
        ApplyPalette(theme.Dark, options.Dark, true);
    }

    private static void ApplyPalette(OrchestratorWebUIThemePaletteOptions source, ZoorceThemePalette target, bool dark)
    {
        target.Primary = source.PrimaryColor;
        target.PrimaryHover = source.PrimaryHoverColor;
        target.Sidebar = source.SidebarBackgroundColor;
        target.SidebarText = source.SidebarTextColor;
        target.SidebarMuted = source.SidebarMutedTextColor;
        target.Canvas = source.ContentBackgroundColor;
        target.Surface = source.SurfaceColor;
        target.Elevated = source.ElevatedColor ?? $"color-mix(in srgb, {source.SurfaceColor} 94%, {source.PrimaryColor})";
        target.Text = source.TextColor;
        target.Muted = source.MutedTextColor;
        target.Border = source.BorderColor;
        target.Subtle = source.SubtleBackgroundColor;
        target.PrimaryForeground = source.PrimaryForegroundColor ?? DefaultOrchestratorThemeCssRenderer.PreferredForeground(source.PrimaryColor, source.SurfaceColor);
        target.PrimarySoft = source.PrimarySoftColor ?? $"color-mix(in srgb, {source.PrimaryColor} 12%, {source.SurfaceColor})";
        target.Glow = source.GlowColor ?? $"color-mix(in srgb, {source.PrimaryColor} {(dark ? 24 : 10)}%, transparent)";
        target.Success = source.SuccessColor;
        target.SuccessSoft = source.SuccessSoftColor;
        target.Warning = source.WarningColor;
        target.WarningSoft = source.WarningSoftColor;
        target.Danger = source.DangerColor;
        target.DangerForeground = source.DangerForegroundColor;
        target.DangerSoft = source.DangerSoftColor;
        target.Info = source.InfoColor;
        target.InfoSoft = source.InfoSoftColor;
        target.DataPrimary = source.DataPrimaryColor;
        target.DataSecondary = source.DataSecondaryColor;
        target.DataTertiary = source.DataTertiaryColor;
        target.DataQuaternary = source.DataQuaternaryColor;
        target.DataQuinary = source.DataQuinaryColor;
        target.CodeCanvas = source.CodeBackgroundColor;
        target.CodeText = source.CodeTextColor;
        target.CodeBorder = source.CodeBorderColor ?? source.BorderColor;
        target.CodeControlSurface = source.CodeControlSurfaceColor ?? source.CodeBackgroundColor;
        target.CodeControlText = source.CodeControlTextColor ?? source.CodeTextColor;
        target.Shadow = source.ShadowColor;
        target.Overlay = source.OverlayColor;
    }
}
