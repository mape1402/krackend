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
}
