using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Zoorce.Web.UI.Layout;
using Zoorce.Web.UI.Theming;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ZoorceShellThemeConfigurationTests
{
    [Theory]
    [InlineData(OrchestratorWebUIThemeMode.Light, ZoorceThemeMode.Light)]
    [InlineData(OrchestratorWebUIThemeMode.Dark, ZoorceThemeMode.Dark)]
    public void HostThemeIsAppliedToBothZoorcePalettes(OrchestratorWebUIThemeMode mode, ZoorceThemeMode expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOrchestratorWebUIShell(options =>
        {
            options.Title = "My orchestration";
            options.Mode = mode;
            options.Light.PrimaryColor = "#123456";
            options.Light.SurfaceColor = "#abcdef";
            options.Light.SubtleBackgroundColor = "#eeeeee";
            options.Light.CodeBackgroundColor = "#fefefe";
            options.Light.CodeTextColor = "#112233";
            options.Light.BorderColor = "#aabbcc";
            options.Dark.PrimaryColor = "#654321";
            options.Dark.TextColor = "#fedcba";
            options.Dark.SurfaceColor = "#102030";
            options.Dark.SubtleBackgroundColor = "#203040";
        });
        services.AddOrchestratorWebUIShell();
        using var provider = services.BuildServiceProvider();

        var theme = provider.GetRequiredService<IOptions<ZoorceShellOptions>>().Value;
        Assert.Equal("My orchestration", theme.ApplicationName);
        Assert.Equal("K", theme.BrandMark);
        Assert.Equal(expected, theme.DefaultTheme);
        Assert.Equal("#123456", theme.Light.Primary);
        Assert.Equal("#abcdef", theme.Light.Surface);
        Assert.Equal("color-mix(in srgb, #abcdef 94%, #123456)", theme.Light.Elevated);
        Assert.Equal("color-mix(in srgb, #123456 12%, #abcdef)", theme.Light.PrimarySoft);
        Assert.Equal("color-mix(in srgb, #123456 10%, transparent)", theme.Light.Glow);
        Assert.Equal("#aabbcc", theme.Light.CodeBorder);
        Assert.Equal("#fefefe", theme.Light.CodeControlSurface);
        Assert.Equal("#112233", theme.Light.CodeControlText);
        Assert.Equal("#654321", theme.Dark.Primary);
        Assert.Equal("#fedcba", theme.Dark.Text);
        Assert.Equal("color-mix(in srgb, #102030 94%, #654321)", theme.Dark.Elevated);
        Assert.Equal("color-mix(in srgb, #654321 12%, #102030)", theme.Dark.PrimarySoft);
        Assert.Equal("color-mix(in srgb, #654321 24%, transparent)", theme.Dark.Glow);
        Assert.Single(provider.GetServices<IConfigureOptions<ZoorceShellOptions>>(),
            item => item is ZoorceShellThemeConfiguration);
    }

    [Fact]
    public void ExplicitPaletteTokensAreCopiedAndAppliedToZoorce()
    {
        var source = new OrchestratorWebUIThemeOptions();
        var fields = new Dictionary<string, string>
        {
            [nameof(source.Light.ElevatedColor)] = "#010101", [nameof(source.Light.PrimaryForegroundColor)] = "#020202",
            [nameof(source.Light.PrimarySoftColor)] = "#030303", [nameof(source.Light.GlowColor)] = "#040404",
            [nameof(source.Light.SuccessColor)] = "#050505", [nameof(source.Light.SuccessSoftColor)] = "#060606",
            [nameof(source.Light.WarningColor)] = "#070707", [nameof(source.Light.WarningSoftColor)] = "#080808",
            [nameof(source.Light.DangerColor)] = "#090909", [nameof(source.Light.DangerForegroundColor)] = "#101010",
            [nameof(source.Light.DangerSoftColor)] = "#111111", [nameof(source.Light.InfoColor)] = "#121212",
            [nameof(source.Light.InfoSoftColor)] = "#131313", [nameof(source.Light.DataPrimaryColor)] = "#141414",
            [nameof(source.Light.DataSecondaryColor)] = "#151515", [nameof(source.Light.DataTertiaryColor)] = "#161616",
            [nameof(source.Light.DataQuaternaryColor)] = "#171717", [nameof(source.Light.DataQuinaryColor)] = "#181818",
            [nameof(source.Light.CodeBorderColor)] = "#191919", [nameof(source.Light.CodeControlSurfaceColor)] = "#202020",
            [nameof(source.Light.CodeControlTextColor)] = "#212121", [nameof(source.Light.OverlayColor)] = "#222222"
        };
        foreach (var (name, value) in fields)
        {
            typeof(OrchestratorWebUIThemePaletteOptions).GetProperty(name)!.SetValue(source.Light, value);
            typeof(OrchestratorWebUIThemePaletteOptions).GetProperty(name)!.SetValue(source.Dark, value);
        }
        var copied = new OrchestratorWebUIThemeOptions();
        copied.ApplyFrom(source);
        var shell = new ZoorceShellOptions();
        new ZoorceShellThemeConfiguration(Options.Create(copied)).Configure(shell);
        foreach (var (name, value) in fields)
        {
            Assert.Equal(value, typeof(OrchestratorWebUIThemePaletteOptions).GetProperty(name)!.GetValue(copied.Light));
            var targetName = name switch { "ElevatedColor" => "Elevated", "PrimaryForegroundColor" => "PrimaryForeground", "CodeControlSurfaceColor" => "CodeControlSurface", "CodeControlTextColor" => "CodeControlText", _ => name[..^5] };
            Assert.Equal(value, typeof(ZoorceThemePalette).GetProperty(targetName)!.GetValue(shell.Light));
            Assert.Equal(value, typeof(ZoorceThemePalette).GetProperty(targetName)!.GetValue(shell.Dark));
        }
    }

    [Fact]
    public void ConfigurationRejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new ZoorceShellThemeConfiguration(null!));
        var configuration = new ZoorceShellThemeConfiguration(Options.Create(new OrchestratorWebUIThemeOptions()));
        Assert.Throws<ArgumentNullException>(() => configuration.Configure(null!));
    }

    [Theory]
    [InlineData("#fff", "#ffffff", "#000000")]
    [InlineData(" #fff ", " #ffffff ", "#000000")]
    [InlineData("#000", "#ffffff", "#ffffff")]
    [InlineData("#00000000", "#ffffff", "#000000")]
    [InlineData("not-a-color", "#ffffff", "#ffffff")]
    [InlineData(null, "#ffffff", "#ffffff")]
    [InlineData("#fff", null, "#ffffff")]
    [InlineData("#fff", "invalid", "#ffffff")]
    public void PrimaryForegroundIsDerivedFromTheActualAccent(string primary, string surface, string expected)
    {
        var theme = new OrchestratorWebUIThemeOptions();
        theme.Light.PrimaryColor = primary;
        theme.Light.SurfaceColor = surface;
        var shell = new ZoorceShellOptions();
        new ZoorceShellThemeConfiguration(Options.Create(theme)).Configure(shell);
        Assert.Equal(expected, shell.Light.PrimaryForeground);
    }
}
