using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Security;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;
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
            item => AssertNavigation(item, "Orchestrations", "OrchestratorDesign", "/Orchestrations/Index", 10),
            item => AssertNavigation(item, "Domains", "OrchestratorDesign", "/Domains/Index", 20),
            item => AssertNavigation(item, "Runtime Nodes", "OrchestratorDistribution", "/RuntimeNodes/Index", 20),
            item => AssertNavigation(item, "Artifacts", "OrchestratorDistribution", "/ArtifactReleases/Index", 22),
            item => AssertNavigation(item, "Releases", "OrchestratorDistribution", "/Promotions/Index", 23),
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

        Assert.Equal(13, options.Conventions.Count);
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
        Assert.Equal("ops", provider.GetRequiredService<IOptions<OrchestratorDesignWebUIOptions>>().Value.RoutePrefix);
        Assert.Equal("knowl", provider.GetRequiredService<IOptions<OrchestratorDesignWebUIOptions>>().Value.DefaultSchemaRegistryProviderKey);
    }

    [Fact]
    public void WebUiModulesRejectNullConfigurationCallbacks()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => DesignWebUiServices.AddOrchestratorDesignWebUI(services, null!));
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
