using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class WebUIStatusPagesIntegrationTests
{
    [Fact]
    public async Task SharedStatusEndpointRendersTheZoorceStatusPage()
    {
        await using var app = await BuildHostAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_orchestrator/status/404");
        request.Headers.Accept.ParseAdd("text/html");

        using var response = await app.GetTestClient().SendAsync(request);

        Assert.Equal(StatusCodes.Status404NotFound, (int)response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("z-status-page", content, StringComparison.Ordinal);
        Assert.Contains("Krackend", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiAndNonGetFailuresRemainOwnedByTheirEndpoints()
    {
        await using var app = await BuildHostAsync();
        using var api = await app.GetTestClient().GetAsync("/api/missing");
        using var mutation = await app.GetTestClient().PostAsync("/missing-browser", content: null);

        Assert.Equal(StatusCodes.Status404NotFound, (int)api.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, (int)mutation.StatusCode);
        Assert.Empty(await api.Content.ReadAsStringAsync());
        Assert.Empty(await mutation.Content.ReadAsStringAsync());
    }

    [Fact]
    public void StatusPageMiddlewareRejectsANullApplicationBuilder()
    {
        Assert.Throws<ArgumentNullException>(() => Krackend.Sagas.Orchestrations.WebUI.Shell.ApplicationBuilderExtensions.UseOrchestratorWebUIStatusPages(null!));
    }

    private static async Task<WebApplication> BuildHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOrchestratorWebUIShell(options =>
        {
            options.Title = "Krackend";
            options.Subtitle = "Control plane";
        });

        var app = builder.Build();
        app.UseOrchestratorWebUIStatusPages();
        app.MapGet("/missing-browser", () => Results.NotFound());
        app.MapPost("/missing-browser", () => Results.NotFound());
        app.MapGet("/api/missing", () => Results.NotFound());
        app.MapRazorPages();
        await app.StartAsync();
        return app;
    }
}
