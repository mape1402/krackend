using System.Net.Http.Json;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
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
}
