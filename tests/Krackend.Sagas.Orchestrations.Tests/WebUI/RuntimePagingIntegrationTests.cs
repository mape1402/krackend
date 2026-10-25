using System.Net.Http.Json;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RuntimeNodesPage = Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.DesignNodes.IndexModel;

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
        { typeof(Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Releases.IndexModel), "OnGetNodeOptionsAsync" }
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
}
