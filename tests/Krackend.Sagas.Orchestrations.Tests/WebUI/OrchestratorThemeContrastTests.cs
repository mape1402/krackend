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
}
