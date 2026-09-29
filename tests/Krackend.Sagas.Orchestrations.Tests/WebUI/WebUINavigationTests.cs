using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Security;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;
using global::ButterMorph.Web.Razor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ControlPlaneWebUiServices = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.ServiceCollectionExtensions;
using DesignWebUiServices = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ServiceCollectionExtensions;
using DistributionWebUiServices = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.ServiceCollectionExtensions;
using RuntimeWebUiServices = Krackend.Sagas.Orchestrations.Runtime.WebUI.ServiceCollectionExtensions;
using SecurityWebUiServices = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Security.ServiceCollectionExtensions;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class WebUINavigationTests
{
    [Fact]
    public void WebUiModulesRegisterNavigationContributorsInDeterministicOrder()
    {
        var services = new ServiceCollection();

        services.AddOrchestratorWebUIShell();
        DesignWebUiServices.AddOrchestratorDesignWebUI(services, options => options.RoutePrefix = "admin");
        DistributionWebUiServices.AddOrchestratorDistributionWebUI(services, options => options.RoutePrefix = "admin/distribution");
        SecurityWebUiServices.AddOrchestratorSecurityWebUI(services, options => options.RoutePrefix = "admin/security");
        RuntimeWebUiServices.AddOrchestratorRuntimeWebUI(services, options => options.RoutePrefix = "runtime");

        using var provider = services.BuildServiceProvider();
        var items = provider.GetRequiredService<OrchestratorNavigationRegistry>().GetItems().ToArray();

        Assert.Collection(
            items,
            item => AssertNavigation(item, "Overview", "OrchestratorDesign", "/Index", 0),
            item => AssertNavigation(item, "Orchestrations", "OrchestratorDesign", "/Orchestrations/Index", 10),
            item => AssertNavigation(item, "Environments", "OrchestratorDistribution", "/Environments/Index", 19),
            item => AssertNavigation(item, "Domains", "OrchestratorDesign", "/Domains/Index", 20),
            item => AssertNavigation(item, "Runtime Nodes", "OrchestratorDistribution", "/RuntimeNodes/Index", 20),
            item => AssertNavigation(item, "Artifacts", "OrchestratorDistribution", "/ArtifactReleases/Index", 22),
            item => AssertNavigation(item, "Releases", "OrchestratorDistribution", "/Promotions/Index", 23),
            item => AssertNavigation(item, "Metadata", "OrchestratorDesign", "/Metadata/Index", 30),
            item => AssertNavigation(item, "Teams", "OrchestratorSecurity", "/Teams/Index", 30),
            item => AssertNavigation(item, "Diagnostics", "OrchestratorRuntime", "/Instances/Index", 35),
            item => AssertNavigation(item, "Artifacts", "OrchestratorRuntime", "/Artifacts/Index", 36),
            item => AssertNavigation(item, "Design Nodes", "OrchestratorRuntime", "/DesignNodes/Index", 37));
    }

    [Fact]
    public void RuntimeWebUiAddsShellWhenRegisteredAlone()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        RuntimeWebUiServices.AddOrchestratorRuntimeWebUI(services);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<OrchestratorNavigationRegistry>());
        Assert.Single(provider.GetServices<IOrchestratorNavigationContributor>());
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRuntimeDiagnosticsReader));
    }

    [Fact]
    public void WebUiShellRegistersDefaultThemeServices()
    {
        var services = new ServiceCollection();

        services.AddOrchestratorWebUIShell();

        using var provider = services.BuildServiceProvider();
        var theme = provider.GetRequiredService<IOptions<OrchestratorWebUIThemeOptions>>().Value;

        Assert.Equal("Orchestrator", theme.Title);
        Assert.Equal(OrchestratorWebUIThemeMode.Light, theme.Mode);
        Assert.Equal("#4856d7", theme.PrimaryColor);
        Assert.IsType<DefaultOrchestratorThemeCssRenderer>(
            provider.GetRequiredService<IOrchestratorThemeCssRenderer>());
        Assert.IsType<DefaultOrchestratorThemeModeAccessor>(
            provider.GetRequiredService<IOrchestratorThemeModeAccessor>());
    }

    [Fact]
    public void ThemeModeAccessorUsesCookieBeforeConfiguredDefault()
    {
        var services = new ServiceCollection();

        services.AddOrchestratorWebUIShell(options => options.Mode = OrchestratorWebUIThemeMode.Light);

        using var provider = services.BuildServiceProvider();
        var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext();
        httpContextAccessor.HttpContext.Request.Headers.Cookie = ".Krackend.Orchestrator.ThemeMode=dark";

        var accessor = provider.GetRequiredService<IOrchestratorThemeModeAccessor>();
        var theme = provider.GetRequiredService<IOptions<OrchestratorWebUIThemeOptions>>().Value;

        Assert.Equal(OrchestratorWebUIThemeMode.Dark, accessor.GetMode(theme));
        Assert.Equal("dark", accessor.GetCssMode(theme));
    }

    [Fact]
    public void ControlPlaneWebUiRegistersConfiguredTheme()
    {
        var services = new ServiceCollection();

        ControlPlaneWebUiServices.AddOrchestratorControlPlaneWebUI(services, options =>
        {
            options.Theme.Title = "Atlas";
            options.Theme.Subtitle = "Control Plane";
            options.Theme.Mode = OrchestratorWebUIThemeMode.Dark;
            options.Theme.PrimaryColor = "#112233";
            options.Theme.Dark.PrimaryColor = "#445566";
            options.Theme.IconCssClass = "bi-box";
        });

        using var provider = services.BuildServiceProvider();
        var theme = provider.GetRequiredService<IOptions<OrchestratorWebUIThemeOptions>>().Value;

        Assert.Equal("Atlas", theme.Title);
        Assert.Equal("Control Plane", theme.Subtitle);
        Assert.Equal(OrchestratorWebUIThemeMode.Dark, theme.Mode);
        Assert.Equal("#112233", theme.PrimaryColor);
        Assert.Equal("#445566", theme.Dark.PrimaryColor);
        Assert.Equal("bi-box", theme.IconCssClass);
    }

    [Fact]
    public void ControlPlaneWebUiPropagatesThemeToButterMorphDesigner()
    {
        var services = new ServiceCollection();

        ControlPlaneWebUiServices.AddOrchestratorControlPlaneWebUI(services, options =>
        {
            options.Theme.Mode = OrchestratorWebUIThemeMode.Dark;
            options.Theme.PrimaryColor = "#0b3d91";
            options.Theme.PrimaryHoverColor = "#082f6f";
            options.Theme.SidebarBackgroundColor = "#061a36";
            options.Theme.SidebarMutedTextColor = "#9fb6d8";
            options.Theme.ContentBackgroundColor = "#f2f6fb";
            options.Theme.SurfaceColor = "#ffffff";
            options.Theme.Light.MutedTextColor = "#52657f";
            options.Theme.Light.BorderColor = "#cbd8e8";
            options.Theme.Light.SubtleBackgroundColor = "#eaf1fa";
            options.Theme.Dark.PrimaryColor = "#73a8ff";
            options.Theme.Dark.PrimaryHoverColor = "#96beff";
            options.Theme.Dark.SidebarBackgroundColor = "#031021";
            options.Theme.Dark.SidebarMutedTextColor = "#9eb7d9";
            options.Theme.Dark.ContentBackgroundColor = "#07111f";
            options.Theme.Dark.SurfaceColor = "#0d1b2f";
            options.Theme.Dark.TextColor = "#edf6ff";
            options.Theme.Dark.MutedTextColor = "#9fb0c8";
            options.Theme.Dark.BorderColor = "#203654";
            options.Theme.Dark.SubtleBackgroundColor = "#10243d";
        });

        using var provider = services.BuildServiceProvider();
        var butterMorphOptions = provider.GetRequiredService<IOptions<ButterMorphRazorDesignerOptions>>().Value;

        Assert.False(butterMorphOptions.ShowSchemaActions);
        Assert.Equal(ButterMorphDesignerThemeMode.Dark, butterMorphOptions.Theme.Mode);
        Assert.Equal(ButterMorphDesignerThemeMode.Dark, butterMorphOptions.Theme.DefaultMode);
        Assert.Equal("#0b3d91", butterMorphOptions.Theme.Light.PrimaryColor);
        Assert.Equal("#082f6f", butterMorphOptions.Theme.Light.PrimaryDarkColor);
        Assert.Equal("#f2f6fb", butterMorphOptions.Theme.Light.BackgroundColor);
        Assert.Equal("#eaf1fa", butterMorphOptions.Theme.Light.SurfaceSoftColor);
        Assert.Equal("#e2eaf2", butterMorphOptions.Theme.Light.BorderColor);
        Assert.Equal("#253956", butterMorphOptions.Theme.Light.SidebarBorderColor);
        Assert.Equal("#73a8ff", butterMorphOptions.Theme.Dark.PrimaryColor);
        Assert.Equal("#96beff", butterMorphOptions.Theme.Dark.PrimaryHoverColor);
        Assert.Equal("#07111f", butterMorphOptions.Theme.Dark.BackgroundColor);
        Assert.Equal("#0d1b2f", butterMorphOptions.Theme.Dark.SurfaceColor);
        Assert.Equal("#10243d", butterMorphOptions.Theme.Dark.SurfaceSoftColor);
        Assert.Equal("#172a43", butterMorphOptions.Theme.Dark.BorderColor);
        Assert.Equal("#223146", butterMorphOptions.Theme.Dark.SidebarBorderColor);
    }

    [Fact]
    public void RuntimeWebUiRegistersConfiguredTheme()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        RuntimeWebUiServices.AddOrchestratorRuntimeWebUI(services, options =>
        {
            options.RoutePrefix = "node";
            options.Theme.Title = "Krackend Node";
            options.Theme.Subtitle = "Runtime";
            options.Theme.PrimaryColor = "#223344";
            options.Theme.Dark.SurfaceColor = "#101820";
        });

        using var provider = services.BuildServiceProvider();
        var runtimeOptions = provider.GetRequiredService<IOptions<OrchestratorRuntimeWebUIOptions>>().Value;
        var theme = provider.GetRequiredService<IOptions<OrchestratorWebUIThemeOptions>>().Value;

        Assert.Equal("node", runtimeOptions.RoutePrefix);
        Assert.Equal("Krackend Node", theme.Title);
        Assert.Equal("Runtime", theme.Subtitle);
        Assert.Equal("#223344", theme.PrimaryColor);
        Assert.Equal("#101820", theme.Dark.SurfaceColor);
    }

    [Fact]
    public void ThemeCssRendererOutputsLightAndDarkPalettes()
    {
        var renderer = new DefaultOrchestratorThemeCssRenderer();
        var theme = new OrchestratorWebUIThemeOptions
        {
            PrimaryColor = "#123456"
        };
        theme.Dark.PrimaryColor = "#abcdef";
        theme.Dark.SurfaceColor = "red";

        var css = renderer.RenderThemeRules(theme);

        Assert.Contains(":root[data-krackend-theme=\"light\"]", css);
        Assert.Contains(":root[data-krackend-theme=\"dark\"]", css);
        Assert.Contains("--krackend-primary:#123456;", css);
        Assert.Contains("--krackend-primary:#abcdef;", css);
        Assert.Contains("--krackend-surface:#171a2f;", css);
        Assert.DoesNotContain("red", css);
    }

    [Fact]
    public void RuntimeWebUiRegistersSignalRReactivePublisher()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        RuntimeWebUiServices.AddOrchestratorRuntimeWebUI(services);

        using var provider = services.BuildServiceProvider();

        Assert.IsType<SignalRRuntimeReactiveEventPublisher>(
            provider.GetRequiredService<IRuntimeReactiveEventPublisher>());
        Assert.NotNull(typeof(RuntimeReactiveEndpointRouteBuilderExtensions)
            .GetMethod(nameof(RuntimeReactiveEndpointRouteBuilderExtensions.MapOrchestratorRuntimeReactiveHub)));
    }

    [Fact]
    public void WebUiModulesConfigureRazorPageAreaRoutes()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        DesignWebUiServices.AddOrchestratorDesignWebUI(services);
        DistributionWebUiServices.AddOrchestratorDistributionWebUI(services);
        SecurityWebUiServices.AddOrchestratorSecurityWebUI(services);
        RuntimeWebUiServices.AddOrchestratorRuntimeWebUI(services);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RazorPagesOptions>>().Value;

        Assert.Equal(19, options.Conventions.Count);
    }

    [Fact]
    public void ControlPlaneWebUiRegistersExpectedDefaultRoutePrefixes()
    {
        var services = new ServiceCollection();

        ControlPlaneWebUiServices.AddOrchestratorControlPlaneWebUI(services);

        using var provider = services.BuildServiceProvider();

        Assert.Equal("admin/design", provider.GetRequiredService<IOptions<OrchestratorDesignWebUIOptions>>().Value.RoutePrefix);
        Assert.Equal("admin/distribution", provider.GetRequiredService<IOptions<OrchestratorDistributionWebUIOptions>>().Value.RoutePrefix);
        Assert.Equal("admin/security", provider.GetRequiredService<IOptions<OrchestratorSecurityWebUIOptions>>().Value.RoutePrefix);
    }

    [Fact]
    public void ControlPlaneWebUiRegistersAggregateDefaultsAndNormalizesOptions()
    {
        var services = new ServiceCollection();

        ControlPlaneWebUiServices.AddOrchestratorControlPlaneWebUI(services, options =>
        {
            options.DesignRoutePrefix = " /control/design/ ";
            options.DistributionRoutePrefix = " /control/distribution/ ";
            options.SecurityRoutePrefix = " /control/security/ ";
            options.DefaultSchemaRegistryProviderKey = " atlas ";
        });

        using var provider = services.BuildServiceProvider();
        var designOptions = provider.GetRequiredService<IOptions<OrchestratorDesignWebUIOptions>>().Value;
        var distributionOptions = provider.GetRequiredService<IOptions<OrchestratorDistributionWebUIOptions>>().Value;
        var securityOptions = provider.GetRequiredService<IOptions<OrchestratorSecurityWebUIOptions>>().Value;

        Assert.Equal("control/design", designOptions.RoutePrefix);
        Assert.Equal("atlas", designOptions.DefaultSchemaRegistryProviderKey);
        Assert.Equal("control/distribution", distributionOptions.RoutePrefix);
        Assert.Equal("control/security", securityOptions.RoutePrefix);
    }

    [Fact]
    public void ControlPlaneAggregateRegistersApplicationStorageAndWebUi()
    {
        var services = new ServiceCollection();

        ControlPlaneWebUiServices.AddOrchestratorControlPlane(services, options =>
        {
            options.AdminRootPath = " /ops/ ";
            options.DefaultSchemaRegistryProviderKey = " ";
            options.ConfigureStorage = db => db.UseInMemoryDatabase($"control-plane-webui-{Guid.NewGuid():N}");
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ControlPlaneDbContext>());
        Assert.NotNull(provider.GetRequiredService<IOrchestrationSchemaContextBuilder>());
        Assert.Equal("ops/design", provider.GetRequiredService<IOptions<OrchestratorDesignWebUIOptions>>().Value.RoutePrefix);
        Assert.Equal("ops/distribution", provider.GetRequiredService<IOptions<OrchestratorDistributionWebUIOptions>>().Value.RoutePrefix);
        Assert.Equal("ops/security", provider.GetRequiredService<IOptions<OrchestratorSecurityWebUIOptions>>().Value.RoutePrefix);
        Assert.Equal("knowl", provider.GetRequiredService<IOptions<OrchestratorDesignWebUIOptions>>().Value.DefaultSchemaRegistryProviderKey);
    }

    [Fact]
    public void WebUiModulesRejectNullConfigurationCallbacks()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => DesignWebUiServices.AddOrchestratorDesignWebUI(services, null!));
        Assert.Throws<ArgumentNullException>(() => services.AddOrchestratorWebUIShell(null!));
        Assert.Throws<ArgumentNullException>(() => RuntimeWebUiServices.AddOrchestratorRuntimeWebUI(services, null!));
        Assert.Throws<ArgumentNullException>(() => ControlPlaneWebUiServices.AddOrchestratorControlPlaneWebUI(services, null!));
        Assert.Throws<ArgumentNullException>(() => ControlPlaneWebUiServices.AddOrchestratorControlPlane(services, null!));
        Assert.Throws<InvalidOperationException>(() => ControlPlaneWebUiServices.AddOrchestratorControlPlane(services, _ => { }));
    }

    private static void AssertNavigation(
        OrchestratorNavigationItem item,
        string label,
        string area,
        string page,
        int order)
    {
        Assert.Equal(label, item.Label);
        Assert.Equal(area, item.Area);
        Assert.Equal(page, item.Page);
        Assert.Equal(order, item.Order);
    }
}
