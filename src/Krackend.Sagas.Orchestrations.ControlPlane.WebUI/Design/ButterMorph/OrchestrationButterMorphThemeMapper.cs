namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>
/// Default mapper from Krackend Web UI theme options to ButterMorph designer theme options.
/// </summary>
internal sealed class OrchestrationButterMorphThemeMapper : IOrchestrationButterMorphThemeMapper
{
    /// <inheritdoc />
    public void Apply(OrchestratorWebUIThemeOptions source, ButterMorphDesignerThemeOptions target)
    {
        if (source is null || target is null)
        {
            return;
        }

        target.Mode = source.Mode == OrchestratorWebUIThemeMode.Dark
            ? ButterMorphDesignerThemeMode.Dark
            : ButterMorphDesignerThemeMode.Light;
        target.DefaultMode = target.Mode;

        ApplyPalette(source.Light, target.Light);
        ApplyPalette(source.Dark, target.Dark);
    }

    private static void ApplyPalette(
        OrchestratorWebUIThemePaletteOptions source,
        ButterMorphDesignerThemePaletteOptions target)
    {
        target.PrimaryColor = source.PrimaryColor;
        target.PrimaryHoverColor = source.PrimaryHoverColor;
        target.PrimaryDarkColor = source.PrimaryHoverColor;
        target.BackgroundColor = source.ContentBackgroundColor;
        target.SurfaceColor = source.SurfaceColor;
        target.SurfaceSoftColor = source.SubtleBackgroundColor;
        target.TextColor = source.TextColor;
        target.MutedTextColor = source.MutedTextColor;
        target.BorderColor = BlendOrDefault(source.BorderColor, source.SurfaceColor, 0.45, source.BorderColor);
        target.StrongBorderColor = BlendOrDefault(source.PrimaryColor, source.BorderColor, 0.35, source.PrimaryColor);
        target.SidebarBackgroundColor = source.SidebarBackgroundColor;
        target.SidebarBrandBackgroundColor = source.SidebarBrandBackgroundColor;
        target.SidebarBorderColor = BlendOrDefault(source.SidebarBackgroundColor, source.SidebarMutedTextColor, 0.20, source.SidebarBackgroundColor);
        target.SidebarTextColor = source.SidebarTextColor;
        target.SidebarMutedTextColor = source.SidebarMutedTextColor;
        target.SidebarActiveTextColor = source.PrimaryColor;
    }

    private static string BlendOrDefault(string from, string to, double weight, string fallback)
        => TryParseHexRgb(from, out var fromRgb) && TryParseHexRgb(to, out var toRgb)
            ? ToHex(
                BlendChannel(fromRgb.Red, toRgb.Red, weight),
                BlendChannel(fromRgb.Green, toRgb.Green, weight),
                BlendChannel(fromRgb.Blue, toRgb.Blue, weight))
            : fallback;

    private static int BlendChannel(int from, int to, double weight)
        => (int)Math.Round(from + ((to - from) * weight), MidpointRounding.AwayFromZero);

    private static bool TryParseHexRgb(string value, out (int Red, int Green, int Blue) rgb)
    {
        rgb = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var color = value.Trim();
        if (color.Length != 7 || color[0] != '#')
        {
            return false;
        }

        try
        {
            rgb = (
                Convert.ToInt32(color.Substring(1, 2), 16),
                Convert.ToInt32(color.Substring(3, 2), 16),
                Convert.ToInt32(color.Substring(5, 2), 16));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ToHex(int red, int green, int blue)
        => $"#{red:x2}{green:x2}{blue:x2}";
}
