using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Json;
using StageDetailsModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.OrchestrationStages.DetailsModel;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class RoadmapElementPackageTests
{
    [Fact]
    public async Task CopiesAllElementTypesBetweenRoadmapsWithoutOriginalReferences()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOrchestratorDesignApplication();
        services.AddOrchestratorControlPlaneStorageEntityFramework(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<ControlPlaneDbContext>().Database.EnsureCreatedAsync();
        var versions = sp.GetRequiredService<IOrchestrationVersionRepository>();
        var stages = sp.GetRequiredService<IStageRepository>();
        var tasks = sp.GetRequiredService<ITaskRepository>();
        var triggers = sp.GetRequiredService<ITriggerBindingRepository>();
        var source = Version();
        var destination = Version();
        await versions.Create(source);
        await versions.Create(destination);
        var stage = new StageDefinition { Id = Id.New(), OrchestrationVersionId = source.Id, Key = "delivery", Name = "Delivery", Order = 0, IsEnabled = false };
        var target = new StageDefinition { Id = Id.New(), OrchestrationVersionId = destination.Id, Key = "existing", Name = "Existing", Order = 0, IsEnabled = true };
        await stages.Create(stage);
        await stages.Create(target);
        var group = new ParallelGroupDefinition { Id = Id.New(), StageDefinitionId = stage.Id, Name = "Parallel", JoinPolicy = ParallelJoinPolicy.WaitAll, MaxParallelAgents = 2 };
        await sp.GetRequiredService<IParallelGroupRepository>().Create(group);
        await sp.GetRequiredService<IBranchRuleRepository>().Create(new BranchRuleDefinition
        {
            Id = Id.New(), FromType = ElementType.Stage, FromId = stage.Id, NavigateToType = ElementType.Stage, NavigateToId = stage.Id,
            Condition = new ExecutionCondition { Engine = EngineType.DSL, Configuration = new DslConditionConfiguration { Expression = new Expression("true") } }
        });
        var task = new TaskDefinition
        {
            Id = Id.New(), StageDefinitionId = stage.Id, Key = "charge", Name = "Charge", Notes = "keep", Order = 0,
            Kind = TaskKind.Messaging, ExecutionMode = TaskExecutionMode.Parallel, ParallelGroupId = group.Id, IsEnabled = true,
            DispatchType = TaskDispatchType.FireAndWaitCallback, Configuration = new MessagingTaskConfiguration { Topic = "payments.charge", Version = new SemanticVersion(1, 2, 3) },
            RetryPolicy = new RetryPolicy { MaxRetries = 2, StrategyType = RetryStrategyType.Fixed, Strategy = new FixedRetryStrategy { Delay = Duration.FromSeconds(5) }, RetryableErrorCodes = ["retry"] },
            TimeoutPolicy = new TimeoutPolicy { Timeout = Duration.FromSeconds(60), TimeoutBehavior = TimeoutBehavior.Fail, TimeoutBehaviorPolicy = new FailTimeoutBehaviorPolicy { ErrorCode = "expired" } },
            ExecutionCondition = new ExecutionCondition { Engine = EngineType.DSL, Configuration = new DslConditionConfiguration { Expression = new Expression("trigger.value") } }, HasExecutionCondition = true,
            Transformation = new TransformationDefinition { Engine = EngineType.DSL, Configuration = new DslTransformationConfiguration { Dsl = "map source to target" } }, HasTransformation = true
        };
        var originalBinding = new SchemaBinding
        {
            Id = Id.New(), ElementType = ElementType.Task, ElementId = task.Id, ContractId = Id.New(),
            ContractKey = "payments.charge", ContractVersion = new SemanticVersion(1, 2, 3),
            RegistryProviderId = Id.New(), RegistryProviderKey = "registry", ContractKind = SchemaContractKind.Command
        };
        ((MessagingTaskConfiguration)task.Configuration).SchemaBinding = originalBinding;
        await tasks.Create(task);
        var trigger = new TriggerBinding { Id = Id.New(), OrchestrationVersionId = source.Id, Key = "created", TriggerType = TriggerType.Event, TriggerChannel = new EventTriggerChannel { Topic = "sales.created", Version = new SemanticVersion(1, 0, 0) }, IsEnabled = true };
        await triggers.Create(trigger);
        var service = sp.GetRequiredService<RoadmapElementPackageService>();

        var taskJson = await service.CopyAsync("task", task.Id.ToString(), source.Id.ToString());
        using (var package = JsonDocument.Parse(taskJson)) Assert.Equal("task", package.RootElement.GetProperty("elementType").GetString());
        var taskId = await service.PasteAsync(taskJson, "task", destination.Id.ToString(), target.Id.ToString());
        var pasted = await tasks.GetById(new Id(Ulid.Parse(taskId)));
        Assert.NotEqual(task.Id, pasted.Id);
        Assert.Equal(target.Id, pasted.StageDefinitionId);
        Assert.Null(pasted.ParallelGroupId);
        Assert.Equal(TaskExecutionMode.Sequential, pasted.ExecutionMode);
        Assert.Null(pasted.ExecutionCondition);
        Assert.Null(pasted.Transformation);
        Assert.Equal("payments.charge", Assert.IsType<MessagingTaskConfiguration>(pasted.Configuration).Topic);
        var copiedBinding = Assert.IsType<MessagingTaskConfiguration>(pasted.Configuration).SchemaBinding;
        Assert.Equal(pasted.Id, copiedBinding.ElementId);
        Assert.NotEqual(originalBinding.Id, copiedBinding.Id);
        Assert.Equal(originalBinding.ContractId, copiedBinding.ContractId);
        Assert.Equal(2, pasted.RetryPolicy.MaxRetries);
        Assert.Equal(Duration.FromSeconds(5), Assert.IsType<FixedRetryStrategy>(pasted.RetryPolicy.Strategy).Delay);
        Assert.Equal(Duration.FromSeconds(60), pasted.TimeoutPolicy.Timeout);
        Assert.Equal("expired", Assert.IsType<FailTimeoutBehaviorPolicy>(pasted.TimeoutPolicy.TimeoutBehaviorPolicy).ErrorCode);
        Assert.Equal(task.Id, Assert.IsType<MessagingTaskConfiguration>((await tasks.GetById(task.Id)).Configuration).SchemaBinding.ElementId);
        var anotherTask = await service.PasteAsync(taskJson, "task", destination.Id.ToString(), target.Id.ToString());
        Assert.Equal("charge_copy", (await tasks.GetById(new Id(Ulid.Parse(anotherTask)))).Key);
        var clonedTask = await service.PasteAsync(taskJson, "task", source.Id.ToString(), stage.Id.ToString());
        Assert.Equal("charge_copy", (await tasks.GetById(new Id(Ulid.Parse(clonedTask)))).Key);
        await tasks.SetExecutionCondition(new Id(Ulid.Parse(clonedTask)), new ExecutionCondition
        {
            Engine = EngineType.DSL, Configuration = new DslConditionConfiguration { Expression = new Expression("true") }
        });

        var originalStage = await stages.GetById(stage.Id);
        Assert.Single(originalStage.ParallelGroups);
        Assert.Single(originalStage.BranchRules);
        var stageJson = await service.CopyAsync("stage", stage.Id.ToString(), source.Id.ToString());
        var stageId = await service.PasteAsync(stageJson, "stage", destination.Id.ToString());
        var copiedStage = await stages.GetById(new Id(Ulid.Parse(stageId)));
        Assert.False(copiedStage.IsEnabled);
        var children = (await tasks.GetAll(copiedStage.Id)).ToArray();
        Assert.Equal(2, children.Length);
        Assert.All(children, child =>
        {
            Assert.Null(child.ParallelGroupId);
            Assert.Equal(TaskExecutionMode.Sequential, child.ExecutionMode);
            Assert.NotEqual(task.Id, child.Id);
        });
        Assert.Equal("true", Assert.IsType<DslConditionConfiguration>(children.Single(x => x.Key == "charge_copy").ExecutionCondition.Configuration).Expression.ToString());
        Assert.Empty(copiedStage.ParallelGroups);
        Assert.Empty(copiedStage.BranchRules);

        var beforeRollback = (await stages.GetAll(destination.Id)).Count();
        var failingTasks = Substitute.For<ITaskRepository>();
        var creates = 0;
        failingTasks.Create(Arg.Any<TaskDefinition>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (++creates == 2) throw new InvalidOperationException("simulated storage failure");
            await tasks.Create(call.Arg<TaskDefinition>(), call.Arg<CancellationToken>());
        });
        var failingService = new RoadmapElementPackageService(stages, failingTasks, triggers,
            sp.GetRequiredService<IOrchestrationVersionEditGuard>(), sp.GetRequiredService<IControlPlaneUnitOfWork>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failingService.PasteAsync(stageJson, "stage", destination.Id.ToString()));
        Assert.Equal(beforeRollback, (await stages.GetAll(destination.Id)).Count());

        var triggerJson = await service.CopyAsync("trigger", trigger.Id.ToString(), source.Id.ToString());
        var triggerId = await service.PasteAsync(triggerJson, "trigger", destination.Id.ToString());
        var copiedTrigger = await triggers.GetById(new Id(Ulid.Parse(triggerId)));
        Assert.NotEqual(trigger.Id, copiedTrigger.Id);
        Assert.Equal("sales.created", Assert.IsType<EventTriggerChannel>(copiedTrigger.TriggerChannel).Topic);
        Assert.True(copiedTrigger.IsEnabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PasteAsync(triggerJson, "stage", destination.Id.ToString()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PasteAsync("bad json", "task", destination.Id.ToString(), target.Id.ToString()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CopyAsync("task", task.Id.ToString(), destination.Id.ToString()));
        var readOnly = Version();
        readOnly.Status = OrchestrationVersionStatus.Deployed;
        readOnly.Version = new SemanticVersion(1, 0, 0);
        await versions.Create(readOnly);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PasteAsync(triggerJson, "trigger", readOnly.Id.ToString()));
    }

    private static OrchestrationVersion Version() => new()
    {
        Id = Id.New(), OrchestrationDefinitionId = Id.New(), Version = null, Status = OrchestrationVersionStatus.Draft,
        Checksum = new Checksum("draft"), CreatedBy = "test", CreatedOnUtc = DateTime.UtcNow,
        DraftSlot = Guid.NewGuid().ToString(), VersionLabel = "", Description = "", Notes = ""
    };

    [Fact]
    public async Task RazorHandlersCopyAndPasteTaskThroughHttp()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOrchestratorDesignApplication();
        builder.Services.AddOrchestratorControlPlaneStorageEntityFramework(options => options.UseSqlite(connection));
        Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ServiceCollectionExtensions.AddOrchestratorDesignWebUI(builder.Services);
        builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
        builder.Services.AddRazorPages(options =>
        {
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/OrchestrationStages/Details", "roadmap-stage");
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/OrchestrationVersions/Details", "roadmap-version");
        })
            .AddApplicationPart(typeof(StageDetailsModel).Assembly);
        await using var app = builder.Build();
        app.MapRazorPages();
        app.MapGet("/test-antiforgery", (HttpContext http, IAntiforgery antiforgery) =>
            Results.Text(antiforgery.GetAndStoreTokens(http).RequestToken));
        var version = Version();
        var stage = new StageDefinition { Id = Id.New(), OrchestrationVersionId = version.Id, Key = "stage", Name = "Stage", IsEnabled = true };
        var task = new TaskDefinition
        {
            Id = Id.New(), StageDefinitionId = stage.Id, Key = "task", Name = "Task", Kind = TaskKind.Messaging,
            Configuration = new MessagingTaskConfiguration { Topic = "commands.test", Version = new SemanticVersion(1, 0, 0) },
            DispatchType = TaskDispatchType.FireAndForget, IsEnabled = true
        };
        var trigger = new TriggerBinding
        {
            Id = Id.New(), OrchestrationVersionId = version.Id, Key = "trigger", TriggerType = TriggerType.Event,
            TriggerChannel = new EventTriggerChannel { Topic = "events.test", Version = new SemanticVersion(1, 0, 0) }, IsEnabled = true
        };
        using (var scope = app.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            await sp.GetRequiredService<ControlPlaneDbContext>().Database.EnsureCreatedAsync();
            await sp.GetRequiredService<IOrchestrationVersionRepository>().Create(version);
            await sp.GetRequiredService<IStageRepository>().Create(stage);
            await sp.GetRequiredService<ITaskRepository>().Create(task);
            await sp.GetRequiredService<ITriggerBindingRepository>().Create(trigger);
        }
        await app.StartAsync();
        using var client = app.GetTestClient();
        var page = await client.GetStringAsync($"/roadmap-stage?orchestrationId={version.OrchestrationDefinitionId}&versionId={version.Id}&stageId={stage.Id}");
        Assert.Contains("Copy task", page);
        Assert.Contains("Clone task", page);
        Assert.Contains("Paste task", page);
        Assert.Contains("od-roadmap-dial", page);
        Assert.Contains("data-paste-target=\"task\"", page);
        Assert.Contains("data-roadmap-toast", page);
        using var source = await client.GetFromJsonAsync<JsonDocument>($"/roadmap-stage?handler=RoadmapElement&elementType=task&elementId={task.Id}&versionId={version.Id}");
        var package = source!.RootElement.GetProperty("package").GetString();
        using var tokenResponse = await client.GetAsync("/test-antiforgery");
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadAsStringAsync();
        var cookie = tokenResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
        using var policyResponse = await client.PostAsync("/roadmap-version?handler=InstanceDurations", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["orchestrationId"] = version.OrchestrationDefinitionId.ToString(),
                ["versionId"] = version.Id.ToString(), ["maxInstanceDurationSeconds"] = "172800", ["idleDurationSeconds"] = "3600" }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, policyResponse.StatusCode);
        using (var policyScope = app.Services.CreateScope())
        {
            var saved = await policyScope.ServiceProvider.GetRequiredService<IOrchestrationVersionRepository>().GetById(version.Id);
            Assert.Equal(172800, saved.MaxInstanceDurationSeconds);
            Assert.Equal(3600, saved.IdleDurationSeconds);
        }
        using var result = await client.PostAsJsonAsync("/roadmap-stage?handler=PasteRoadmapElement",
            new RoadmapElementPasteRequest(package, "task", version.Id.ToString(), stage.Id.ToString()));
        result.EnsureSuccessStatusCode();
        using var copy = await result.Content.ReadFromJsonAsync<JsonDocument>();
        using var verify = app.Services.CreateScope();
        var copiedId = new Id(Ulid.Parse(copy!.RootElement.GetProperty("id").GetString()));
        Assert.Equal("task_copy", (await verify.ServiceProvider.GetRequiredService<ITaskRepository>().GetById(copiedId)).Key);
        foreach (var (type, sourceId) in new[] { ("stage", stage.Id), ("trigger", trigger.Id) })
        {
            using var sourceElement = await client.GetFromJsonAsync<JsonDocument>($"/roadmap-version?handler=RoadmapElement&elementType={type}&elementId={sourceId}&versionId={version.Id}");
            var json = sourceElement!.RootElement.GetProperty("package").GetString();
            using var response = await client.PostAsJsonAsync("/roadmap-version?handler=PasteRoadmapElement",
                new RoadmapElementPasteRequest(json, type, version.Id.ToString(), ""));
            response.EnsureSuccessStatusCode();
            using var created = await response.Content.ReadFromJsonAsync<JsonDocument>();
            var id = new Id(Ulid.Parse(created!.RootElement.GetProperty("id").GetString()));
            if (type == "stage") Assert.Equal("stage_copy", (await verify.ServiceProvider.GetRequiredService<IStageRepository>().GetById(id)).Key);
            else Assert.Equal("trigger_copy", (await verify.ServiceProvider.GetRequiredService<ITriggerBindingRepository>().GetById(id)).Key);
        }
    }
}
