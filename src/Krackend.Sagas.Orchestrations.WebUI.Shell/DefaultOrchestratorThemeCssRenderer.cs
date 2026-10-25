using System.Text;
using System.Text.RegularExpressions;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>
/// Default CSS variable renderer for Krackend orchestration Web UI themes.
/// </summary>
public sealed class DefaultOrchestratorThemeCssRenderer : IOrchestratorThemeCssRenderer
{
    private readonly OrchestratorWebUIThemeOptions _defaults = new();
    private readonly Regex _cssColorRegex = new("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);

    /// <inheritdoc />
    public string RenderThemeRules(OrchestratorWebUIThemeOptions options)
    {
        var theme = options ?? _defaults;
        var builder = new StringBuilder();

        builder.Append(":root{");
        AppendPalette(builder, theme.Light, _defaults.Light, "light");
        builder.AppendLine("}");

        builder.Append(":root[data-krackend-theme=\"light\"]{");
        AppendPalette(builder, theme.Light, _defaults.Light, "light");
        builder.AppendLine("}");

        builder.Append(":root[data-krackend-theme=\"dark\"]{");
        AppendPalette(builder, theme.Dark, _defaults.Dark, "dark");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private void AppendPalette(
        StringBuilder builder,
        OrchestratorWebUIThemePaletteOptions palette,
        OrchestratorWebUIThemePaletteOptions fallback,
        string colorScheme)
    {
        AppendColor(builder, "--krackend-primary", palette.PrimaryColor, fallback.PrimaryColor);
        AppendColor(builder, "--krackend-primary-hover", palette.PrimaryHoverColor, fallback.PrimaryHoverColor);
        var surface = NormalizeColor(palette.SurfaceColor, fallback.SurfaceColor);
        var onPrimary = ContrastColor(NormalizeColor(palette.PrimaryColor, fallback.PrimaryColor), surface);
        builder.Append("--krackend-on-primary:").Append(NormalizeColor(palette.PrimaryForegroundColor, onPrimary)).Append(';');
        builder.Append("--krackend-on-primary-hover:").Append(ContrastColor(NormalizeColor(palette.PrimaryHoverColor, fallback.PrimaryHoverColor), surface)).Append(';');
        AppendColor(builder, "--krackend-sidebar-bg", palette.SidebarBackgroundColor, fallback.SidebarBackgroundColor);
        AppendColor(builder, "--krackend-sidebar-brand-bg", palette.SidebarBrandBackgroundColor, fallback.SidebarBrandBackgroundColor);
        AppendColor(builder, "--krackend-sidebar-text", palette.SidebarTextColor, fallback.SidebarTextColor);
        AppendColor(builder, "--krackend-sidebar-muted", palette.SidebarMutedTextColor, fallback.SidebarMutedTextColor);
        AppendColor(builder, "--krackend-content-bg", palette.ContentBackgroundColor, fallback.ContentBackgroundColor);
        AppendColor(builder, "--krackend-surface", palette.SurfaceColor, fallback.SurfaceColor);
        AppendColor(builder, "--krackend-text", palette.TextColor, fallback.TextColor);
        AppendColor(builder, "--krackend-muted-text", palette.MutedTextColor, fallback.MutedTextColor);
        AppendColor(builder, "--krackend-border", palette.BorderColor, fallback.BorderColor);
        AppendColor(builder, "--krackend-subtle-bg", palette.SubtleBackgroundColor, fallback.SubtleBackgroundColor);
        AppendColor(builder, "--krackend-code-bg", palette.CodeBackgroundColor, fallback.CodeBackgroundColor);
        AppendColor(builder, "--krackend-code-text", palette.CodeTextColor, fallback.CodeTextColor);
        AppendColor(builder, "--krackend-shadow-color", palette.ShadowColor, fallback.ShadowColor);
        builder.Append("--krackend-color-scheme:").Append(colorScheme).Append(';');
    }

    private void AppendColor(StringBuilder builder, string variableName, string value, string fallback)
        => builder.Append(variableName).Append(':').Append(NormalizeColor(value, fallback)).Append(';');

    private string NormalizeColor(string value, string fallback)
        => !string.IsNullOrWhiteSpace(value) && _cssColorRegex.IsMatch(value.Trim())
            ? value.Trim()
            : fallback;

    private static string ContrastColor(string foreground, string background)
    {
        var foregroundRgb = ParseColor(foreground);
        var backgroundRgb = ParseColor(background);
        var alpha = foregroundRgb.Alpha / 255d;
        var red = (foregroundRgb.Red * alpha) + (backgroundRgb.Red * (1 - alpha));
        var green = (foregroundRgb.Green * alpha) + (backgroundRgb.Green * (1 - alpha));
        var blue = (foregroundRgb.Blue * alpha) + (backgroundRgb.Blue * (1 - alpha));
        var luminance = (0.2126 * Linearize(red)) + (0.7152 * Linearize(green)) + (0.0722 * Linearize(blue));
        return luminance > 0.1791287847 ? "#000000" : "#ffffff";
    }

    internal static string PreferredForeground(string foreground, string background)
    {
        const string pattern = "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$";
        foreground = foreground?.Trim();
        background = background?.Trim();
        return Regex.IsMatch(foreground ?? string.Empty, pattern) && Regex.IsMatch(background ?? string.Empty, pattern)
            ? ContrastColor(foreground, background) : "#ffffff";
    }

    private static (int Red, int Green, int Blue, int Alpha) ParseColor(string color)
    {
        var hex = color.AsSpan(1);
        if (hex.Length == 3)
        {
            return (Convert.ToInt32(hex[..1].ToString(), 16) * 17,
                Convert.ToInt32(hex.Slice(1, 1).ToString(), 16) * 17,
                Convert.ToInt32(hex.Slice(2, 1).ToString(), 16) * 17, 255);
        }

        return (Convert.ToInt32(hex[..2].ToString(), 16),
            Convert.ToInt32(hex.Slice(2, 2).ToString(), 16),
            Convert.ToInt32(hex.Slice(4, 2).ToString(), 16),
            hex.Length == 8 ? Convert.ToInt32(hex.Slice(6, 2).ToString(), 16) : 255);
    }

    private static double Linearize(double value)
    {
        var channel = value / 255d;
        return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }
}
