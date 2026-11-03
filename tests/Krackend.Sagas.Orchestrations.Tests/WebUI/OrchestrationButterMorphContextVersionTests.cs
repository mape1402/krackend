using ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;
using NSubstitute;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using StageDetailsModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.OrchestrationStages.DetailsModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class OrchestrationButterMorphContextVersionTests
{
    [Theory]
    [InlineData("orchestration-task-transform", true)]
    [InlineData("orchestration-task-execution-condition", false)]
    [InlineData("orchestration-task-compensation-transform", true)]
    [InlineData("orchestration-task-compensation-execution-condition", false)]
    [InlineData("orchestration-trigger-compensation-transform", true)]
    [InlineData("orchestration-trigger-compensation-execution-condition", false)]
    [InlineData("orchestration-stage-execution-condition", false)]
    public async Task SessionChangesWhenSchemaOrSavedDesignChanges(string kind, bool mapping)
    {
        var schema = new OrchestrationSchemaContext
        {
            OrchestrationVersionId = "version", OrchestrationVersion = "1.0.0",
            StageKey = "stage", TaskKey = "task", Signature = new string('a', 64)
        };
        var schemas = Substitute.For<IOrchestrationSchemaContextApplicationService>();
        schemas.GetForTask(Arg.Any<GetTaskSchemaContextQuery>(), Arg.Any<CancellationToken>()).Returns(_ => schema);
        schemas.GetForStage(Arg.Any<GetStageSchemaContextQuery>(), Arg.Any<CancellationToken>()).Returns(_ => schema);
        schemas.GetForTaskCompensation(Arg.Any<GetTaskCompensationSchemaContextQuery>(), Arg.Any<CancellationToken>()).Returns(_ => schema);
        schemas.GetForTriggerCompensation(Arg.Any<GetTriggerCompensationSchemaContextQuery>(), Arg.Any<CancellationToken>()).Returns(_ => schema);
        var dsl = "original design";
        var mappingHost = Substitute.For<IButterMorphDesignerHost>();
        mappingHost.Load(Arg.Any<ButterMorphDesignerLoadRequest>()).Returns(_ => new ButterMorphDesignerLoadResult { InitialDslContent = dsl });
        var validationHost = Substitute.For<IButterMorphValidationDesignerHost>();
        validationHost.Load(Arg.Any<ButterMorphValidationDesignerLoadRequest>()).Returns(_ => new ButterMorphValidationDesignerLoadResult { InitialDslContent = dsl });
        var parser = new OrchestrationButterMorphDesignerContextParser();
        var context = $"{kind}:version:element";

        var first = await OrchestrationButterMorphContextVersion.ResolveAsync(context, parser, schemas, mappingHost, validationHost);
        Assert.Equal(first, await OrchestrationButterMorphContextVersion.ResolveAsync(context, parser, schemas, mappingHost, validationHost));
        Assert.Equal(4, first.Split(':').Length);
        if (mapping)
            Assert.True(kind.Contains("trigger") ? parser.TryParseTriggerCompensationTransformation(first, out _) :
                kind.Contains("compensation") ? parser.TryParseTaskCompensationTransformation(first, out _) : parser.TryParseTaskTransformation(first, out _));

        schema = schema with { Signature = new string('b', 64) };
        var changedSchema = await OrchestrationButterMorphContextVersion.ResolveAsync(context, parser, schemas, mappingHost, validationHost);
        Assert.NotEqual(first, changedSchema);
        dsl = "saved new design";
        schema = schema with { Signature = new string('a', 64) };
        var revertedSchema = await OrchestrationButterMorphContextVersion.ResolveAsync(context, parser, schemas, mappingHost, validationHost);
        Assert.NotEqual(first, revertedSchema);
    }

    [Fact]
    public void ParserRejectsInvalidSessionVersions()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();
        Assert.False(parser.TryParseTaskTransformation("orchestration-task-transform:version:task:invalid", out _));
        Assert.False(parser.TryParseTaskTransformation($"orchestration-task-transform:version:task:{new string('z', 64)}", out _));
        Assert.True(parser.TryParseTaskTransformation($"orchestration-task-transform:version:task:{new string('a', 64)}", out _));
    }

    [Fact]
    public async Task RazorHandlerReturnsNewContextAfterSchemaChanges()
    {
        var schema = new OrchestrationSchemaContext
        {
            OrchestrationVersionId = "version", OrchestrationVersion = "1.0.0",
            StageKey = "stage", TaskKey = "task", Signature = new string('a', 64)
        };
        var schemas = Substitute.For<IOrchestrationSchemaContextApplicationService>();
        schemas.GetForTask(Arg.Any<GetTaskSchemaContextQuery>(), Arg.Any<CancellationToken>()).Returns(_ => schema);
        var mappingHost = Substitute.For<IButterMorphDesignerHost>();
        mappingHost.Load(Arg.Any<ButterMorphDesignerLoadRequest>()).Returns(new ButterMorphDesignerLoadResult { InitialDslContent = "saved" });
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(schemas);
        builder.Services.AddSingleton<IOrchestrationButterMorphDesignerContextParser, OrchestrationButterMorphDesignerContextParser>();
        builder.Services.AddSingleton(mappingHost);
        builder.Services.AddSingleton(Substitute.For<IButterMorphValidationDesignerHost>());
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
        const string url = "/stage?handler=ButterMorphContext&context=orchestration-task-transform%3Aversion%3Atask";
        using var first = await client.GetFromJsonAsync<JsonDocument>(url);
        schema = schema with { Signature = new string('b', 64) };
        using var second = await client.GetFromJsonAsync<JsonDocument>(url);
        Assert.NotEqual(first!.RootElement.GetProperty("context").GetString(), second!.RootElement.GetProperty("context").GetString());
    }

    [Fact]
    public async Task ButterMorphReloadsMappingAndValidationForANewVersionedContext()
    {
        var mapping = Substitute.For<IButterMorphDesignerHost>();
        mapping.Load(Arg.Any<ButterMorphDesignerLoadRequest>()).Returns(new ButterMorphDesignerLoadResult());
        var validation = Substitute.For<IButterMorphValidationDesignerHost>();
        validation.Load(Arg.Any<ButterMorphValidationDesignerLoadRequest>()).Returns(new ButterMorphValidationDesignerLoadResult());
        var services = new ServiceCollection();
        services.AddLogging();
        Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ServiceCollectionExtensions.AddOrchestratorDesignWebUI(services);
        services.RemoveAll<IButterMorphDesignerHost>();
        services.RemoveAll<IButterMorphValidationDesignerHost>();
        services.AddSingleton(mapping);
        services.AddSingleton(validation);
        await using var provider = services.BuildServiceProvider();
        var unique = Guid.NewGuid().ToString("N");

        PageContext Context(string kind, char version)
        {
            var http = new DefaultHttpContext { RequestServices = provider };
            http.Request.QueryString = QueryString.Create("context", $"{kind}:{unique}:task:{new string(version, 64)}");
            return new PageContext { HttpContext = http };
        }

        foreach (var version in new[] { 'a', 'a', 'b' })
        {
            var model = ActivatorUtilities.CreateInstance<DesignerModel>(provider);
            model.PageContext = Context("orchestration-task-transform", version);
            await model.OnGet();
            var condition = ActivatorUtilities.CreateInstance<ValidationDesignerModel>(provider);
            condition.PageContext = Context("orchestration-task-execution-condition", version);
            await condition.OnGet();
        }
        await mapping.Received(2).Load(Arg.Any<ButterMorphDesignerLoadRequest>());
        await validation.Received(2).Load(Arg.Any<ButterMorphValidationDesignerLoadRequest>());
    }
}
