using Krackend.Sagas.Orchestrations.WebUI.Shell;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class OrchestratorThemeContrastTests
{
    [Theory]
    [InlineData("#000", "#ffffff")]
    [InlineData("#fff", "#000000")]
    [InlineData("#73a8ff", "#000000")]
    [InlineData("#003366", "#ffffff")]
    [InlineData("#FFFFFF00", "#ffffff")]
    [InlineData("#FFFFFF80", "#000000")]
    public void SelectsReadableTextForConfiguredPrimary(string primary, string expected)
    {
        var options = new OrchestratorWebUIThemeOptions();
        options.Dark.PrimaryColor = primary;
        options.Dark.SurfaceColor = "#000000";

        var rules = new DefaultOrchestratorThemeCssRenderer().RenderThemeRules(options);
        var dark = rules[(rules.IndexOf(":root[data-krackend-theme=\"dark\"]", StringComparison.Ordinal))..];

        Assert.Contains($"--krackend-on-primary:{expected};", dark, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculatesHoverAndPrimarySeparatelyAndFallsBackFromInvalidColors()
    {
        var options = new OrchestratorWebUIThemeOptions();
        options.Light.PrimaryColor = "invalid";
        options.Light.PrimaryHoverColor = "#ffffff";

        var rules = new DefaultOrchestratorThemeCssRenderer().RenderThemeRules(options);

        Assert.Contains("--krackend-on-primary-hover:#000000;", rules, StringComparison.Ordinal);
        Assert.DoesNotContain("--krackend-primary:invalid;", rules, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultSemanticColorsRemainReadableInBothModes()
    {
        var options = new OrchestratorWebUIThemeOptions();
        foreach (var palette in new[] { options.Light, options.Dark })
        {
            Assert.True(Contrast(palette.TextColor, palette.SurfaceColor) >= 4.5);
            Assert.True(Contrast(palette.SidebarTextColor, palette.SidebarBackgroundColor) >= 4.5);
            Assert.True(Contrast(palette.SidebarMutedTextColor, palette.SidebarBackgroundColor) >= 4.5);
            Assert.True(Contrast(palette.SuccessColor, palette.SuccessSoftColor) >= 4.5);
            Assert.True(Contrast(palette.WarningColor, palette.WarningSoftColor) >= 4.5);
            Assert.True(Contrast(palette.DangerColor, palette.DangerSoftColor) >= 4.5);
            Assert.True(Contrast(palette.InfoColor, palette.InfoSoftColor) >= 4.5);
        }
    }

    private static double Contrast(string first, string second)
    {
        static double Luminance(string color)
        {
            static double Channel(string hex)
            {
                var value = Convert.ToInt32(hex, 16) / 255d;
                return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(color.Substring(1, 2)) + .7152 * Channel(color.Substring(3, 2)) + .0722 * Channel(color.Substring(5, 2));
        }
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
}
