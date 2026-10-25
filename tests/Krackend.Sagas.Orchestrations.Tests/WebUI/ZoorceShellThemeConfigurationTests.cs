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
            options.Dark.PrimaryColor = "#654321";
            options.Dark.TextColor = "#fedcba";
        });
        services.AddOrchestratorWebUIShell();
        using var provider = services.BuildServiceProvider();

        var theme = provider.GetRequiredService<IOptions<ZoorceShellOptions>>().Value;
        Assert.Equal("My orchestration", theme.ApplicationName);
        Assert.Equal("K", theme.BrandMark);
        Assert.Equal(expected, theme.DefaultTheme);
        Assert.Equal("#123456", theme.Light.Primary);
        Assert.Equal("#abcdef", theme.Light.Surface);
        Assert.Equal("#654321", theme.Dark.Primary);
        Assert.Equal("#fedcba", theme.Dark.Text);
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
