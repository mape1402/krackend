using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Design.WebUI;
using Krackend.Sagas.Orchestrations.Distribution.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Security.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;
using Microsoft.Extensions.DependencyInjection;
using DesignWebUiServices = Krackend.Sagas.Orchestrations.Design.WebUI.ServiceCollectionExtensions;
using DistributionWebUiServices = Krackend.Sagas.Orchestrations.Distribution.WebUI.ServiceCollectionExtensions;
using RuntimeWebUiServices = Krackend.Sagas.Orchestrations.Runtime.WebUI.ServiceCollectionExtensions;
using SecurityWebUiServices = Krackend.Sagas.Orchestrations.Security.WebUI.ServiceCollectionExtensions;

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
            item => AssertNavigation(item, "Environments", "OrchestratorDistribution", "/Environments/Index", 20),
            item => AssertNavigation(item, "Runtime Nodes", "OrchestratorDistribution", "/RuntimeNodes/Index", 21),
            item => AssertNavigation(item, "Artifacts", "OrchestratorDistribution", "/ArtifactReleases/Index", 22),
            item => AssertNavigation(item, "Releases", "OrchestratorDistribution", "/Promotions/Index", 23),
            item => AssertNavigation(item, "Teams", "OrchestratorSecurity", "/Teams/Index", 30),
            item => AssertNavigation(item, "Instances", "OrchestratorRuntime", "/Instances/Index", 35),
            item => AssertNavigation(item, "Artifacts", "OrchestratorRuntime", "/Artifacts/Index", 40));
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
