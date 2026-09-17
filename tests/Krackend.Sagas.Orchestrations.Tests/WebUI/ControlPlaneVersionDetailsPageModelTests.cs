using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Reflection;
using VersionDetailsModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.OrchestrationVersions.DetailsModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneVersionDetailsPageModelTests
{
    [Fact]
    public async Task OnGetRedirectsWhenRouteValuesAreMissing()
    {
        var context = CreateContext();

        var result = await context.Page.OnGetAsync("", "version-1");

        Assert.IsType<RedirectToPageResult>(result);
    }

    [Fact]
    public async Task OnGetLoadsDefinitionVersionStagesTaskCountsAndTriggers()
    {
        var context = CreateContext();

        var result = await context.Page.OnGetAsync("orch-1", "version-1");

        Assert.IsType<PageResult>(result);
        Assert.True(context.Page.CanEdit);
        Assert.Equal("Sale Created", context.Page.Orchestration.Name);
        Assert.Equal("1.0.0", context.Page.SelectedVersion.Version);
        Assert.Equal(["stage-0", "stage-1"], context.Page.Stages.Select(x => x.Id));
        Assert.Equal(2, context.Page.StageTaskCounts["stage-0"]);
        Assert.Single(context.Page.TriggerBindings);
    }

    [Fact]
    public async Task OnGetReturnsNotFoundWhenVersionCannotBeLoaded()
    {
        var context = CreateContext();
        context.VersionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OrchestrationVersionModel>(null!));

        var result = await context.Page.OnGetAsync("orch-1", "missing");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TransitionVersionInvokesRequestedApplicationCommand()
    {
        var context = CreateContext();

        foreach (var action in new[] { "SetInReview", "ReturnToDraft", "ReopenReview", "Approve", "Deploy", "Deprecate", "Archive", "Unknown" })
        {
            var result = await context.Page.OnPostTransitionVersionAsync("orch-1", "version-1", action);
            Assert.IsType<RedirectToPageResult>(result);
        }

        await context.VersionService.Received(1).SetInReview(Arg.Any<SetOrchestrationVersionInReviewCommand>(), Arg.Any<CancellationToken>());
        await context.VersionService.Received(1).ReturnToDraft(Arg.Any<ReturnOrchestrationVersionToDraftCommand>(), Arg.Any<CancellationToken>());
        await context.VersionService.Received(1).ReopenReview(Arg.Any<ReopenOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>());
        await context.VersionService.Received(1).Approve(Arg.Any<ApproveOrchestrationVersionCommand>(), Arg.Any<CancellationToken>());
        await context.VersionService.Received(1).Deploy(Arg.Any<DeployOrchestrationVersionCommand>(), Arg.Any<CancellationToken>());
        await context.VersionService.Received(1).Deprecate(Arg.Any<DeprecateOrchestrationVersionCommand>(), Arg.Any<CancellationToken>());
        await context.VersionService.Received(1).Archive(Arg.Any<ArchiveOrchestrationVersionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertStageCreatesUpdatesAndHandlesInvalidInput()
    {
        var context = CreateContext();
        context.Page.NewStage = new VersionDetailsModel.CreateStageInput
        {
            Key = "fulfillment",
            Name = "Fulfillment",
            Description = "First stage"
        };

        Assert.IsType<RedirectToPageResult>(await context.Page.OnPostUpsertStageAsync("orch-1", "version-1"));
        await context.StageService.Received(1).Create(Arg.Is<CreateStageDefinitionCommand>(x => x.Order == 2), Arg.Any<CancellationToken>());

        context.Page.NewStage = new VersionDetailsModel.CreateStageInput
        {
            StageId = "stage-0",
            Key = "validated",
            Name = "Validated",
            Description = "Updated"
        };

        Assert.IsType<RedirectToPageResult>(await context.Page.OnPostUpsertStageAsync("orch-1", "version-1"));
        await context.StageService.Received(1).Update(Arg.Is<UpdateStageDefinitionCommand>(x => x.Key == "validated"), Arg.Any<CancellationToken>());

        var invalid = CreateContext();
        invalid.Page.ModelState.AddModelError("NewStage.Key", "required");
        Assert.IsType<PageResult>(await invalid.Page.OnPostUpsertStageAsync("orch-1", "version-1"));
    }

    [Fact]
    public async Task StageEditorConditionDeleteAndReorderHandlersUseApplicationServices()
    {
        var context = CreateContext();

        Assert.IsType<BadRequestResult>(await context.Page.OnGetStageForEditAsync(""));
        Assert.IsType<JsonResult>(await context.Page.OnGetStageForEditAsync("stage-0"));
        Assert.IsType<BadRequestResult>(await context.Page.OnGetStageExecutionConditionForEditAsync(""));
        Assert.IsType<JsonResult>(await context.Page.OnGetStageExecutionConditionForEditAsync("stage-0"));
        Assert.IsType<BadRequestResult>(await context.Page.OnPostSetStageExecutionConditionAsync(null!));
        Assert.IsType<JsonResult>(await context.Page.OnPostSetStageExecutionConditionAsync(new VersionDetailsModel.SetStageExecutionConditionRequest
        {
            StageId = "stage-0",
            HasExecutionCondition = true,
            ConditionEngine = EngineType.DSL.ToString(),
            ConditionDslExpression = "$trigger.Valid"
        }));
        Assert.IsType<RedirectToPageResult>(await context.Page.OnPostDeleteStageAsync("orch-1", "version-1", "stage-1"));
        Assert.IsType<BadRequestResult>(await context.Page.OnPostReorderStagesAsync(null!));
        Assert.IsType<JsonResult>(await context.Page.OnPostReorderStagesAsync(new VersionDetailsModel.ReorderStagesRequest
        {
            VersionId = "version-1",
            StageIds = ["stage-1", "stage-0", "missing"]
        }));

        await context.StageService.Received().SetExecutionCondition(Arg.Any<SetStageExecutionConditionCommand>(), Arg.Any<CancellationToken>());
        await context.StageService.Received(1).Delete(Arg.Any<DeleteStageDefinitionCommand>(), Arg.Any<CancellationToken>());
        await context.StageService.Received().Update(Arg.Is<UpdateStageDefinitionCommand>(x => x.Id == "stage-1" && x.Order == 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StageHandlersNormalizeOrderAndRedirectWhenEditedStageIsMissing()
    {
        var context = CreateContext();
        context.StageService.GetAll(Arg.Any<GetStageDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StageDefinitionModel>>(
            [
                CreateStage("stage-late", 20),
                CreateStage("stage-early", 10)
            ]));

        await InvokePrivateTaskAsync(context.Page, "NormalizeStageOrderAsync", "version-1", CancellationToken.None);

        await context.StageService.Received(1).Update(
            Arg.Is<UpdateStageDefinitionCommand>(command => command.Id == "stage-early" && command.Order == 0),
            Arg.Any<CancellationToken>());
        await context.StageService.Received(1).Update(
            Arg.Is<UpdateStageDefinitionCommand>(command => command.Id == "stage-late" && command.Order == 1),
            Arg.Any<CancellationToken>());

        context.Page.NewStage = new VersionDetailsModel.CreateStageInput
        {
            StageId = "missing-stage",
            Key = "missing",
            Name = "Missing"
        };
        var result = await context.Page.OnPostUpsertStageAsync("orch-1", "version-1", CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
    }

    [Fact]
    public async Task TriggerHandlersCreateUpdateToggleDeleteAndValidateInput()
    {
        var context = CreateContext();
        context.Page.TriggerInput = new VersionDetailsModel.UpsertTriggerInput
        {
            Key = "sales.sale.created",
            TriggerType = TriggerType.Event.ToString(),
            EventTopic = "events.sales.sale.created",
            EventVersion = "1.1.0",
            HasEventSchemaValidation = true,
            EventSchemaContractKey = "sales.sale.created",
            EventSchemaContractVersion = "2.0.0",
            EventSchemaRegistryProviderId = "01JMJGBJ0R7WFN9QBG3CCBEVM1",
            EventSchemaStrictMode = true,
            HasEventValidation = true,
            EventValidationDsl = "$payload.saleId != null",
            EventValidationErrorCode = "SaleInvalid"
        };

        Assert.IsType<RedirectToPageResult>(await context.Page.OnPostUpsertTriggerAsync("orch-1", "version-1"));
        await context.TriggerService.Received(1).Create(
            Arg.Is<CreateTriggerBindingCommand>(x => IsExpectedTriggerCreateCommand(x)),
            Arg.Any<CancellationToken>());

        context.Page.TriggerInput.TriggerId = "trigger-1";
        context.Page.TriggerInput.Description = "updated";
        Assert.IsType<RedirectToPageResult>(await context.Page.OnPostUpsertTriggerAsync("orch-1", "version-1"));
        await context.TriggerService.Received(1).Update(Arg.Any<UpdateTriggerBindingCommand>(), Arg.Any<CancellationToken>());

        Assert.IsType<RedirectToPageResult>(await context.Page.OnPostDeleteTriggerAsync("orch-1", "version-1", "trigger-1"));
        Assert.IsType<BadRequestResult>(await context.Page.OnPostSetTriggerEnabledAsync(null!));
        Assert.IsType<JsonResult>(await context.Page.OnPostSetTriggerEnabledAsync(new VersionDetailsModel.SetTriggerEnabledRequest { TriggerId = "trigger-1", IsEnabled = false }));
        Assert.IsType<JsonResult>(await context.Page.OnPostSetTriggerEnabledAsync(new VersionDetailsModel.SetTriggerEnabledRequest { TriggerId = "trigger-1", IsEnabled = true }));
        Assert.IsType<BadRequestResult>(await context.Page.OnGetTriggerForEditAsync(""));
        Assert.IsType<JsonResult>(await context.Page.OnGetTriggerForEditAsync("trigger-1"));

        var invalid = CreateContext();
        invalid.Page.TriggerInput = new VersionDetailsModel.UpsertTriggerInput
        {
            Key = "sales.sale.created",
            TriggerType = TriggerType.Event.ToString(),
            EventTopic = "",
            HasEventSchemaValidation = true,
            EventSchemaContractKey = "",
            EventValidationDsl = ""
        };
        Assert.IsType<PageResult>(await invalid.Page.OnPostUpsertTriggerAsync("orch-1", "version-1"));
        Assert.False(invalid.Page.ModelState.IsValid);
    }

    [Fact]
    public async Task TriggerHandlersUseFallbacksAndRedirectWhenEditedTriggerIsMissing()
    {
        var context = CreateContext();
        var triggerTypes = context.Page.TriggerTypes.ToArray();
        var engineTypes = context.Page.EngineTypes.ToArray();

        Assert.Equal(TriggerType.Event.ToString(), triggerTypes.Single().Value);
        Assert.Equal(EngineType.DSL.ToString(), engineTypes.Single().Value);

        context.Page.TriggerInput = new VersionDetailsModel.UpsertTriggerInput
        {
            TriggerId = "missing-trigger",
            Key = "sales.sale.created",
            TriggerType = "Unknown",
            EventTopic = " events.sales.sale.created ",
            EventVersion = "bad",
            EventSchemaRegistryProviderId = "",
            EventSchemaContractKey = " sales.sale.created ",
            EventSchemaContractVersion = "bad",
            HasEventSchemaValidation = false,
            HasEventValidation = false
        };

        var result = await context.Page.OnPostUpsertTriggerAsync("orch-1", "version-1", CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);

        var fallbackCondition = InvokePrivateStatic<ExecutionCondition>(
            "BuildExecutionCondition",
            EngineType.Plugin.ToString(),
            string.Empty);
        var fallbackChannel = InvokePrivateStatic<ITriggerChannel>(
            "BuildTriggerChannel",
            (TriggerType)999,
            context.Page.TriggerInput,
            "not-a-ulid",
            string.Empty);
        var fallbackSchema = InvokePrivateStatic<SchemaBinding>(
            "CreateSchemaBinding",
            " contract ",
            "not-semver",
            "not-a-ulid",
            true,
            true,
            "not-a-ulid",
            " provider ");

        Assert.Equal("true", ((DslConditionConfiguration)fallbackCondition.Configuration).Expression.ToString());
        var eventChannel = Assert.IsType<EventTriggerChannel>(fallbackChannel);
        Assert.Equal("events.sales.sale.created", eventChannel.Topic);
        Assert.Equal("1.0.0", eventChannel.Version.ToString());
        Assert.Equal("contract", fallbackSchema.ContractKey);
        Assert.Equal("1.0.0", fallbackSchema.ContractVersion.ToString());
        Assert.Equal(" provider ", fallbackSchema.RegistryProviderKey);
    }

    [Fact]
    public void AllowedActionsFollowVersionLifecycle()
    {
        var page = CreateContext().Page;

        Assert.Equal(["SetInReview"], page.GetAllowedActions(OrchestrationVersionStatus.Draft));
        Assert.Equal(["Approve", "ReturnToDraft"], page.GetAllowedActions(OrchestrationVersionStatus.InReview));
        Assert.Equal(["Deploy", "ReopenReview"], page.GetAllowedActions(OrchestrationVersionStatus.Approved));
        Assert.Equal(["Deprecate"], page.GetAllowedActions(OrchestrationVersionStatus.Deployed));
        Assert.Equal(["Archive"], page.GetAllowedActions(OrchestrationVersionStatus.Deprecated));
        Assert.Empty(page.GetAllowedActions(OrchestrationVersionStatus.Archived));
    }

    private static TestContext CreateContext()
    {
        var orchestrationService = Substitute.For<IOrchestrationApplicationService>();
        var versionService = Substitute.For<IOrchestrationVersionApplicationService>();
        var stageService = Substitute.For<IStageApplicationService>();
        var taskService = Substitute.For<ITaskApplicationService>();
        var triggerService = Substitute.For<ITriggerBindingApplicationService>();
        var stages = new[] { CreateStage("stage-1", 1), CreateStage("stage-0", 0) };
        var trigger = CreateTrigger();

        orchestrationService.GetById(Arg.Any<GetOrchestrationDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateOrchestration()));
        versionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateVersion()));
        stageService.GetAll(Arg.Any<GetStageDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StageDefinitionModel>>(stages));
        stageService.GetById(Arg.Any<GetStageDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetStageDefinitionByIdQuery>();
                return Task.FromResult(stages.FirstOrDefault(x => x.Id == query.Id)!);
            });
        taskService.GetAll(Arg.Any<GetTaskDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetTaskDefinitionsQuery>();
                var count = query.StageDefinitionId == "stage-0" ? 2 : 1;
                return Task.FromResult<IEnumerable<TaskDefinitionModel>>(
                    Enumerable.Range(0, count).Select(i => new TaskDefinitionModel { Id = $"task-{query.StageDefinitionId}-{i}" }).ToArray());
            });
        triggerService.GetAll(Arg.Any<GetTriggerBindingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<TriggerBindingModel>>([trigger]));
        triggerService.GetById(Arg.Any<GetTriggerBindingByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(trigger));
        stageService.Create(Arg.Any<CreateStageDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("stage-new"));
        stageService.Update(Arg.Any<UpdateStageDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        stageService.Delete(Arg.Any<DeleteStageDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        stageService.SetExecutionCondition(Arg.Any<SetStageExecutionConditionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        triggerService.Create(Arg.Any<CreateTriggerBindingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("trigger-new"));
        triggerService.Update(Arg.Any<UpdateTriggerBindingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        triggerService.Delete(Arg.Any<DeleteTriggerBindingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        triggerService.Enable(Arg.Any<EnableTriggerBindingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        triggerService.Disable(Arg.Any<DisableTriggerBindingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        versionService.SetInReview(Arg.Any<SetOrchestrationVersionInReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        versionService.ReturnToDraft(Arg.Any<ReturnOrchestrationVersionToDraftCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        versionService.ReopenReview(Arg.Any<ReopenOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        versionService.Approve(Arg.Any<ApproveOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        versionService.Deploy(Arg.Any<DeployOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        versionService.Deprecate(Arg.Any<DeprecateOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        versionService.Archive(Arg.Any<ArchiveOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var page = new VersionDetailsModel(
            orchestrationService,
            versionService,
            stageService,
            taskService,
            triggerService,
            Options.Create(new OrchestratorDesignWebUIOptions { DefaultSchemaRegistryProviderKey = "knowl" }));

        return new TestContext(page, versionService, stageService, triggerService);
    }

    private static OrchestrationDefinitionModel CreateOrchestration()
        => new()
        {
            Id = "orch-1",
            Name = "Sale Created",
            Key = "sales.sale.created",
            DomainId = "sales",
            Description = "Happy path"
        };

    private static OrchestrationVersionModel CreateVersion()
        => new()
        {
            Id = "version-1",
            OrchestrationDefinitionId = "orch-1",
            Version = "1.0.0",
            VersionLabel = "v1",
            Status = OrchestrationVersionStatus.Draft
        };

    private static StageDefinitionModel CreateStage(string id, int order)
        => new()
        {
            Id = id,
            OrchestrationVersionId = "version-1",
            Key = $"stage.{order}",
            Name = $"Stage {order}",
            Description = $"Stage {order}",
            Order = order,
            HasExecutionCondition = true,
            ExecutionCondition = new ExecutionCondition
            {
                Engine = EngineType.DSL,
                Configuration = new DslConditionConfiguration { Expression = new Expression("$trigger.Valid") }
            }
        };

    private static TriggerBindingModel CreateTrigger()
        => new()
        {
            Id = "trigger-1",
            OrchestrationVersionId = "version-1",
            Key = "sales.sale.created",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            Description = "Trigger",
            TriggerChannel = new EventTriggerChannel
            {
                Topic = "events.sales.sale.created",
                Version = new SemanticVersion(1, 0, 0),
                HasValidation = true,
                Validation = new ValidationDefinition
                {
                    Engine = EngineType.DSL,
                    ErrorCode = "SaleInvalid",
                    Configuration = new DslValidationConfiguration { Dsl = "$payload.saleId != null" }
                },
                HasSchemaValidation = true,
                SchemaBinding = new SchemaBinding
                {
                    ElementType = ElementType.Orchestration,
                    ElementId = new Id(Ulid.Parse("01JMJGBJ0R7WFN9QBG3CCBEVM2")),
                    ContractId = new Id(Ulid.Parse("01JMJGBJ0R7WFN9QBG3CCBEVM3")),
                    RegistryProviderId = new Id(Ulid.Parse("01JMJGBJ0R7WFN9QBG3CCBEVM1")),
                    RegistryProviderKey = "knowl",
                    ContractKey = "sales.sale.created",
                    ContractVersion = new SemanticVersion(1, 0, 0),
                    StrictMode = true
                }
            }
        };

    private static bool IsExpectedTriggerCreateCommand(CreateTriggerBindingCommand command)
    {
        var channel = command.TriggerChannel as EventTriggerChannel;
        return command.Key == "sales.sale.created" &&
               channel is not null &&
               channel.Topic == "events.sales.sale.created" &&
               channel.Version.ToString() == "1.1.0" &&
               channel.SchemaBinding is not null &&
               channel.SchemaBinding.RegistryProviderKey == "knowl";
    }

    private static Task InvokePrivateTaskAsync(VersionDetailsModel model, string methodName, params object?[] args)
    {
        var method = typeof(VersionDetailsModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(model, args)!;
    }

    private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
    {
        var method = typeof(VersionDetailsModel).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        return (T)method.Invoke(null, args)!;
    }

    private sealed record TestContext(
        VersionDetailsModel Page,
        IOrchestrationVersionApplicationService VersionService,
        IStageApplicationService StageService,
        ITriggerBindingApplicationService TriggerService);
}
