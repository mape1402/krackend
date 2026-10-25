using System.Net.Http.Json;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using ITeamApplicationService = Krackend.Sagas.Orchestrations.ControlPlane.Application.Security.ITeamApplicationService;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using DomainsIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Domains.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneDomainsPagingIntegrationTests
{
    [Fact]
    public async Task PageQueryDoesNotConflictWithRazorPageRoute()
    {
        var service = Substitute.For<IDomainApplicationService>();
        service.GetAll(Arg.Any<GetDomainsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var settings = call.Arg<GetDomainsQuery>().PagedSettings;
                return Task.FromResult(new ApplicationPagedResult<DomainModel>(settings.PageNumber, 2, 30,
                    settings.PageSize, [new DomainModel { Id = $"page-{settings.PageNumber}", Key = "test", DisplayName = "Test" }]));
            });
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(service);
        builder.Services.AddRazorPages(options =>
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Domains/Index", "domains"))
            .AddApplicationPart(typeof(DomainsIndexModel).Assembly);
        await using var app = builder.Build();
        app.MapRazorPages();
        await app.StartAsync();

        using var response = await app.GetTestClient().GetFromJsonAsync<System.Text.Json.JsonDocument>("/domains?handler=List&page=2&pageSize=25");
        Assert.NotNull(response);
        Assert.Equal("page-2", response.RootElement.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.False(response.RootElement.GetProperty("hasMore").GetBoolean());
        await service.Received(1).GetAll(Arg.Is<GetDomainsQuery>(q => q.PagedSettings.PageNumber == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MetadataPageQueryUsesQueryStringInsteadOfRazorRoute()
    {
        var service = Substitute.For<IMetadataDescriptorApplicationService>();
        service.GetAll(Arg.Any<GetMetadataDescriptorsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var settings = call.Arg<GetMetadataDescriptorsQuery>().PagedSettings;
                return Task.FromResult(new ApplicationPagedResult<MetadataDescriptorModel>(settings.PageNumber, 2, 30,
                    settings.PageSize, [new MetadataDescriptorModel { Id = $"page-{settings.PageNumber}", Key = "test", DisplayName = "Test" }]));
            });
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(service);
        builder.Services.AddRazorPages(options =>
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Metadata/Index", "metadata"))
            .AddApplicationPart(typeof(DomainsIndexModel).Assembly);
        await using var app = builder.Build();
        app.MapRazorPages();
        await app.StartAsync();

        using var response = await app.GetTestClient().GetFromJsonAsync<System.Text.Json.JsonDocument>("/metadata?handler=List&page=2&pageSize=25");
        Assert.NotNull(response);
        Assert.Equal("page-2", response.RootElement.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.False(response.RootElement.GetProperty("hasMore").GetBoolean());
        await service.Received(1).GetAll(Arg.Is<GetMetadataDescriptorsQuery>(q => q.PagedSettings.PageNumber == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrchestrationCatalogBindsPageAndReturnsDetailLink()
    {
        var service = Substitute.For<IOrchestrationApplicationService>();
        service.GetAll(Arg.Any<GetOrchestrationDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var settings = call.Arg<GetOrchestrationDefinitionsQuery>().Settings;
                return Task.FromResult(new ApplicationPagedResult<OrchestrationDefinitionModel>(settings.PageNumber, 2, 30,
                    settings.PageSize, [new OrchestrationDefinitionModel { Id = "orchestration-1", Key = "sale", Name = "Sale" }]));
            });
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(service);
        builder.Services.AddSingleton(Substitute.For<IDomainApplicationService>());
        builder.Services.AddSingleton(Substitute.For<ITeamApplicationService>());
        builder.Services.AddRazorPages(options =>
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Orchestrations/Index", "orchestrations"))
            .AddApplicationPart(typeof(DomainsIndexModel).Assembly);
        await using var app = builder.Build();
        app.MapRazorPages();
        await app.StartAsync();

        using var response = await app.GetTestClient().GetFromJsonAsync<System.Text.Json.JsonDocument>("/orchestrations?handler=List&page=2&pageSize=25&search=Sale");
        Assert.NotNull(response);
        Assert.Equal("orchestration-1", response.RootElement.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("Sale", response.RootElement.GetProperty("search").GetString());
        await service.Received(1).GetAll(Arg.Is<GetOrchestrationDefinitionsQuery>(q => q.Settings.PageNumber == 2 && q.SearchText == "Sale"), Arg.Any<CancellationToken>());
    }
}
