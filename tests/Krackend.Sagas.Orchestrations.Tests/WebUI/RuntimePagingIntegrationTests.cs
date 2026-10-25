using System.Net.Http.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RuntimeNodesPage = Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.DesignNodes.IndexModel;
using RuntimeArtifactsPage = Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Artifacts.IndexModel;
using TeamsPage = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Security.Areas.OrchestratorSecurity.Pages.Teams.IndexModel;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class RuntimePagingIntegrationTests
{
    public static TheoryData<Type, string> PaginatedHandlers => new()
    {
        { typeof(RuntimeNodesPage), "OnGetNodesAsync" },
        { typeof(Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Artifacts.IndexModel), "OnGetGroupsAsync" },
        { typeof(Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Artifacts.IndexModel), "OnGetVersionsAsync" },
        { typeof(Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Artifacts.IndexModel), "OnGetHistoryAsync" },
        { typeof(Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Orchestrations.DetailsModel), "OnGetVersionsAsync" },
        { typeof(Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Releases.IndexModel), "OnGetArtifactOptionsAsync" },
        { typeof(Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Releases.IndexModel), "OnGetNodeOptionsAsync" },
        { typeof(TeamsPage), "OnGetMemberListAsync" }
    };

    [Theory]
    [MemberData(nameof(PaginatedHandlers))]
    public void PageParametersExplicitlyBindFromTheQueryString(Type model, string handler)
    {
        var parameter = Assert.Single(model.GetMethod(handler)!.GetParameters(), p => p.Name == "page");
        var binding = Assert.Single(parameter.GetCustomAttributes(typeof(FromQueryAttribute), false).Cast<FromQueryAttribute>());
        Assert.Equal("page", binding.Name);
    }

    [Fact]
    public async Task TeamMemberListBindsPageAndSearchFromQuery()
    {
        var service = Substitute.For<ITeamApplicationService>();
        service.SearchMembers(Arg.Any<SearchTeamMembersQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new ApplicationPagedResult<TeamMemberModel>(call.Arg<SearchTeamMembersQuery>().PageNumber, 2, 30, 25, []));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(service);
        builder.Services.AddRazorPages(options =>
            options.Conventions.AddAreaPageRoute("OrchestratorSecurity", "/Teams/Index", "teams"))
            .AddApplicationPart(typeof(TeamsPage).Assembly);
        await using var app = builder.Build();
        app.MapRazorPages();
        await app.StartAsync();

        using var response = await app.GetTestClient().GetFromJsonAsync<System.Text.Json.JsonDocument>(
            "/teams?handler=MemberList&teamId=example&page=2&pageSize=25&search=alice");
        Assert.NotNull(response);
        Assert.False(response.RootElement.GetProperty("hasMore").GetBoolean());
        await service.Received(1).SearchMembers(Arg.Is<SearchTeamMembersQuery>(x =>
            x.TeamId == "example" && x.PageNumber == 2 && x.PageSize == 25 && x.SearchText == "alice"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesignNodesBindPageFromQueryInsteadOfRazorRoute()
    {
        var repository = Substitute.For<IRuntimeDesignNodeRepository, IRuntimeDesignNodeUiCatalog>();
        var catalog = (IRuntimeDesignNodeUiCatalog)repository;
        catalog.GetPage(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new RuntimeDesignNodePage([], 30, call.ArgAt<int>(0), call.ArgAt<int>(1)));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(repository);
        builder.Services.AddSingleton(Substitute.For<IControlPlaneArtifactPullService>());
        builder.Services.AddSingleton(Substitute.For<IRuntimeDesignNodeConnectionService>());
        builder.Services.AddRazorPages(options =>
            options.Conventions.AddAreaPageRoute("OrchestratorRuntime", "/DesignNodes/Index", "nodes"))
            .AddApplicationPart(typeof(RuntimeNodesPage).Assembly);
        await using var app = builder.Build();
        app.MapRazorPages();
        await app.StartAsync();

        using var response = await app.GetTestClient().GetFromJsonAsync<System.Text.Json.JsonDocument>(
            "/nodes?handler=Nodes&page=2&pageSize=25&search=example");
        Assert.NotNull(response);
        Assert.False(response.RootElement.GetProperty("hasMore").GetBoolean());
        await catalog.Received(1).GetPage(2, 25, "example", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArtifactListsAndHistoryBindPageFromQuery()
    {
        var repository = Substitute.For<IRuntimeArtifactRepository, IRuntimeArtifactUiCatalog>();
        var catalog = (IRuntimeArtifactUiCatalog)repository;
        catalog.GetGroups(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new RuntimeArtifactGroupPage([], 30, call.ArgAt<int>(0), call.ArgAt<int>(1)));
        catalog.GetVersions(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new RuntimeArtifactVersionPage([], 30, call.ArgAt<int>(1), call.ArgAt<int>(2)));
        var historyRepository = Substitute.For<IRuntimeArtifactDeploymentHistoryRepository, IRuntimeArtifactHistoryCatalog>();
        var history = (IRuntimeArtifactHistoryCatalog)historyRepository;
        history.GetPage(Arg.Any<Id>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new RuntimeArtifactHistoryPage([], 30, call.ArgAt<int>(1), call.ArgAt<int>(2)));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(repository);
        builder.Services.AddSingleton(historyRepository);
        builder.Services.AddSingleton(Substitute.For<IRuntimeArtifactReadyNotifier>());
        builder.Services.AddRazorPages(options =>
            options.Conventions.AddAreaPageRoute("OrchestratorRuntime", "/Artifacts/Index", "artifacts"))
            .AddApplicationPart(typeof(RuntimeArtifactsPage).Assembly);
        await using var app = builder.Build();
        app.MapRazorPages();
        await app.StartAsync();
        var id = Id.New();

        foreach (var url in new[]
        {
            "/artifacts?handler=Groups&page=2&pageSize=25&search=sale",
            "/artifacts?handler=Versions&page=2&pageSize=25&orchestrationKey=sales&search=sale",
            $"/artifacts?handler=History&page=2&pageSize=25&artifactId={id}"
        })
        {
            using var response = await app.GetTestClient().GetFromJsonAsync<System.Text.Json.JsonDocument>(url);
            Assert.NotNull(response);
            Assert.False(response.RootElement.GetProperty("hasMore").GetBoolean());
        }
        await catalog.Received(1).GetGroups(2, 25, "sale", Arg.Any<CancellationToken>());
        await catalog.Received(1).GetVersions("sales", 2, 25, "sale", Arg.Any<CancellationToken>());
        await history.Received(1).GetPage(id, 2, 25, Arg.Any<CancellationToken>());
    }
}
