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
        options.UseThemePreset(ZoorceThemePreset.Neutral);
        options.DefaultTheme = theme.Mode == OrchestratorWebUIThemeMode.Dark
            ? ZoorceThemeMode.Dark : ZoorceThemeMode.Light;
        var defaults = new OrchestratorWebUIThemeOptions();
        ApplyPalette(theme.Light, defaults.Light, options.Light, false);
        ApplyPalette(theme.Dark, defaults.Dark, options.Dark, true);
    }

    private static void ApplyPalette(OrchestratorWebUIThemePaletteOptions source, OrchestratorWebUIThemePaletteOptions defaults, ZoorceThemePalette target, bool dark)
    {
        var defaultAccent = string.Equals(source.PrimaryColor, defaults.PrimaryColor, StringComparison.OrdinalIgnoreCase);
        var defaultSurface = string.Equals(source.SurfaceColor, defaults.SurfaceColor, StringComparison.OrdinalIgnoreCase);
        var defaultCode = string.Equals(source.CodeBackgroundColor, defaults.CodeBackgroundColor, StringComparison.OrdinalIgnoreCase)
            && string.Equals(source.CodeTextColor, defaults.CodeTextColor, StringComparison.OrdinalIgnoreCase)
            && string.Equals(source.BorderColor, defaults.BorderColor, StringComparison.OrdinalIgnoreCase);
        if (defaultAccent && defaultSurface)
        {
            target.Elevated = dark ? "#1c2433" : "#ffffff";
            target.PrimaryForeground = dark ? "#102038" : "#ffffff";
            target.PrimarySoft = dark ? "#263a55" : "#e4efff";
        }
        if (defaultAccent) target.Glow = dark ? "rgba(94, 155, 245, 0.20)" : "rgba(36, 95, 199, 0.10)";
        target.Primary = source.PrimaryColor;
        target.PrimaryHover = source.PrimaryHoverColor;
        target.Sidebar = source.SidebarBackgroundColor;
        target.SidebarText = source.SidebarTextColor;
        target.SidebarMuted = source.SidebarMutedTextColor;
        target.Canvas = source.ContentBackgroundColor;
        target.Surface = source.SurfaceColor;
        target.Elevated = source.ElevatedColor ?? (defaultAccent && defaultSurface ? target.Elevated : $"color-mix(in srgb, {source.SurfaceColor} 94%, {source.PrimaryColor})");
        target.Text = source.TextColor;
        target.Muted = source.MutedTextColor;
        target.Border = source.BorderColor;
        target.Subtle = source.SubtleBackgroundColor;
        target.PrimaryForeground = source.PrimaryForegroundColor ?? (defaultAccent && defaultSurface ? target.PrimaryForeground : DefaultOrchestratorThemeCssRenderer.PreferredForeground(source.PrimaryColor, source.SurfaceColor));
        target.PrimarySoft = source.PrimarySoftColor ?? (defaultAccent && defaultSurface ? target.PrimarySoft : $"color-mix(in srgb, {source.PrimaryColor} 12%, {source.SurfaceColor})");
        target.Glow = source.GlowColor ?? (defaultAccent ? target.Glow : $"color-mix(in srgb, {source.PrimaryColor} {(dark ? 24 : 10)}%, transparent)");
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
        target.CodeBorder = source.CodeBorderColor ?? (defaultCode ? target.CodeBorder : source.BorderColor);
        target.CodeControlSurface = source.CodeControlSurfaceColor ?? (defaultCode ? target.CodeControlSurface : source.CodeBackgroundColor);
        target.CodeControlText = source.CodeControlTextColor ?? (defaultCode ? target.CodeControlText : source.CodeTextColor);
        target.Shadow = string.Equals(source.ShadowColor, defaults.ShadowColor, StringComparison.OrdinalIgnoreCase) ? target.Shadow : source.ShadowColor;
        target.Overlay = string.Equals(source.OverlayColor, defaults.OverlayColor, StringComparison.OrdinalIgnoreCase) ? target.Overlay : source.OverlayColor;
    }
}
