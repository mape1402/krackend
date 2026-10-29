using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Zoorce.Web.UI.Abstractions.Navigation;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ZoorceNavigationProviderTests
{
    [Fact]
    public void GroupsRegisteredPagesAndKeepsOverviewSeparate()
    {
        var contributor = Substitute.For<IOrchestratorNavigationContributor>();
        contributor.GetItems().Returns(new[]
        {
            new OrchestratorNavigationItem { Label = "Overview", Area = "OrchestratorDesign", Page = "/Index", Order = 0 },
            new OrchestratorNavigationItem { Label = "Orchestrations", Area = "OrchestratorDesign", Page = "/Orchestrations/Index", Order = 1 },
            new OrchestratorNavigationItem { Label = "Releases", Area = "OrchestratorDistribution", Page = "/Promotions/Index", Order = 2 }
        });
        var links = Substitute.For<LinkGenerator>();
        links.GetPathByAddress(Arg.Any<HttpContext>(), Arg.Any<RouteValuesAddress>(),
            Arg.Any<RouteValueDictionary>(), Arg.Any<RouteValueDictionary>(),
            Arg.Any<PathString?>(), Arg.Any<FragmentString>(), Arg.Any<LinkOptions>())
            .Returns(call => "/" + ((RouteValueDictionary)call[2])["page"]?.ToString()?.Trim('/'));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var provider = new ZoorceOrchestratorNavigationProvider(new OrchestratorNavigationRegistry([contributor]), links, accessor);

        var result = provider.GetItems();
        Assert.Equal(new[] { "Overview", "Design", "Distribution" }, result.Select(x => x.Text));
        Assert.Equal(NavigationMatchMode.Exact, result[0].MatchMode);
        Assert.Equal("Orchestrations", Assert.Single(result[1].Children!).Text);
        Assert.Equal("Releases", Assert.Single(result[2].Children!).Text);
    }

    [Fact]
    public void NoRequestReturnsNoNavigation()
    {
        var provider = new ZoorceOrchestratorNavigationProvider(
            new OrchestratorNavigationRegistry([]), Substitute.For<LinkGenerator>(), new HttpContextAccessor());
        Assert.Empty(provider.GetItems());
    }

    [Fact]
    public void PromotesRuntimeSectionsWithoutARedundantRuntimeParent()
    {
        var contributor = Substitute.For<IOrchestratorNavigationContributor>();
        contributor.GetItems().Returns(new[]
        {
            new OrchestratorNavigationItem { Label = "Dashboard", Area = "OrchestratorRuntime", Page = "/Dashboard/Index", Section = "Activity", Order = 0 },
            new OrchestratorNavigationItem { Label = "Instances", Area = "OrchestratorRuntime", Page = "/Instances/Index", Section = "Activity", Order = 1 },
            new OrchestratorNavigationItem { Label = "Artifacts", Area = "OrchestratorRuntime", Page = "/Artifacts/Index", Section = "Configuration", Order = 2 }
        });
        var links = Substitute.For<LinkGenerator>();
        links.GetPathByAddress(Arg.Any<HttpContext>(), Arg.Any<RouteValuesAddress>(),
            Arg.Any<RouteValueDictionary>(), Arg.Any<RouteValueDictionary>(),
            Arg.Any<PathString?>(), Arg.Any<FragmentString>(), Arg.Any<LinkOptions>())
            .Returns(call => "/" + ((RouteValueDictionary)call[2])["page"]?.ToString()?.Trim('/'));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var provider = new ZoorceOrchestratorNavigationProvider(new OrchestratorNavigationRegistry([contributor]), links, accessor);

        var result = provider.GetItems();

        Assert.Equal(new[] { "Activity", "Configuration" }, result.Select(x => x.Text));
        Assert.Equal(new[] { "Dashboard", "Instances" }, result[0].Children!.Select(x => x.Text));
        Assert.Equal("Artifacts", Assert.Single(result[1].Children!).Text);
        Assert.DoesNotContain(result, x => x.Text == "Runtime");
    }
}
