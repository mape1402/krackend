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
        Assert.Equal("#abcdef", theme.Light.Elevated);
        Assert.Equal("#eeeeee", theme.Light.PrimarySoft);
        Assert.Equal("color-mix(in srgb, #123456 24%, transparent)", theme.Light.Glow);
        Assert.Equal("#aabbcc", theme.Light.CodeBorder);
        Assert.Equal("#fefefe", theme.Light.CodeControlSurface);
        Assert.Equal("#112233", theme.Light.CodeControlText);
        Assert.Equal("#654321", theme.Dark.Primary);
        Assert.Equal("#fedcba", theme.Dark.Text);
        Assert.Equal("#102030", theme.Dark.Elevated);
        Assert.Equal("#203040", theme.Dark.PrimarySoft);
        Assert.Equal("color-mix(in srgb, #654321 24%, transparent)", theme.Dark.Glow);
        Assert.Single(provider.GetServices<IConfigureOptions<ZoorceShellOptions>>(),
            item => item is ZoorceShellThemeConfiguration);
    }

    [Fact]
    public void ConfigurationRejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new ZoorceShellThemeConfiguration(null!));
        var configuration = new ZoorceShellThemeConfiguration(Options.Create(new OrchestratorWebUIThemeOptions()));
        Assert.Throws<ArgumentNullException>(() => configuration.Configure(null!));
    }
}
