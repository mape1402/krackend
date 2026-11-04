namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageDetailsModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.OrchestrationStages.DetailsModel;

public sealed class OrchestrationButterMorphContextCompatibilityTests
{
    [Fact]
    public void ParserAcceptsStableAndPreviousVersionedContexts()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();
        Assert.True(parser.TryParseTaskTransformation("orchestration-task-transform:version:task", out _));
        Assert.True(parser.TryParseTaskTransformation($"orchestration-task-transform:version:task:{new string('a', 64)}", out _));
        Assert.False(parser.TryParseTaskTransformation("orchestration-task-transform:version:task:invalid", out _));
    }

    [Fact]
    public async Task PreviousPageReceivesStableContextWithoutSchemaQueries()
    {
        var schemas = Substitute.For<IOrchestrationSchemaContextApplicationService>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(schemas);
        builder.Services.AddSingleton<IOrchestrationButterMorphDesignerContextParser, OrchestrationButterMorphDesignerContextParser>();
        builder.Services.AddSingleton(Substitute.For<IStageApplicationService>());
        builder.Services.AddSingleton(Substitute.For<ITaskApplicationService>());
        builder.Services.AddSingleton(Substitute.For<IParallelGroupApplicationService>());
        builder.Services.AddSingleton(Substitute.For<IOrchestrationVersionApplicationService>());
        builder.Services.AddSingleton(Substitute.For<ISchemaContractCatalog>());
        builder.Services.AddOptions();
        builder.Services.AddRazorPages(options =>
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/OrchestrationStages/Details", "stage"))
            .AddApplicationPart(typeof(StageDetailsModel).Assembly);
        await using var app = builder.Build();
        app.MapRazorPages();
        await app.StartAsync();
        var client = app.GetTestClient();

        using var result = await client.GetFromJsonAsync<JsonDocument>("/stage?handler=ButterMorphContext&context=orchestration-task-transform%3Aversion%3Atask");
        Assert.Equal("orchestration-task-transform:version:task", result!.RootElement.GetProperty("context").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/stage?handler=ButterMorphContext&context=invalid")).StatusCode);
        await schemas.DidNotReceiveWithAnyArgs().GetForTask(default!);
    }
}
