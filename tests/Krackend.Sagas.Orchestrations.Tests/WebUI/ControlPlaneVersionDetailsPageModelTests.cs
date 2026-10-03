using System.ComponentModel.DataAnnotations;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Reflection;
using System.Text.Json;
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
    public async Task ConstructorRejectsMissingServicesAndUsesFallbackProviderKey()
    {
        var orchestrationService = Substitute.For<IOrchestrationApplicationService>();
        var versionService = Substitute.For<IOrchestrationVersionApplicationService>();
        var stageService = Substitute.For<IStageApplicationService>();
        var taskService = Substitute.For<ITaskApplicationService>();
        var triggerService = Substitute.For<ITriggerBindingApplicationService>();
        var catalog = Substitute.For<ISchemaContractCatalog>();
        catalog.SearchAsync(Arg.Any<SchemaContractCatalogSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<SchemaContractCatalogItem>>([]));
        var options = Options.Create(new OrchestratorDesignWebUIOptions());

        Assert.Equal("orchestrationService", Assert.Throws<ArgumentNullException>(() => new VersionDetailsModel(
            null!,
            versionService,
            stageService,
            taskService,
            triggerService,
            catalog,
            options)).ParamName);
        Assert.Equal("versionService", Assert.Throws<ArgumentNullException>(() => new VersionDetailsModel(
            orchestrationService,
            null!,
            stageService,
            taskService,
            triggerService,
            catalog,
            options)).ParamName);
        Assert.Equal("stageService", Assert.Throws<ArgumentNullException>(() => new VersionDetailsModel(
            orchestrationService,
            versionService,
            null!,
            taskService,
            triggerService,
            catalog,
            options)).ParamName);
        Assert.Equal("taskService", Assert.Throws<ArgumentNullException>(() => new VersionDetailsModel(
            orchestrationService,
            versionService,
            stageService,
            null!,
            triggerService,
            catalog,
            options)).ParamName);
        Assert.Equal("triggerBindingService", Assert.Throws<ArgumentNullException>(() => new VersionDetailsModel(
            orchestrationService,
            versionService,
            stageService,
            taskService,
            null!,
            catalog,
            options)).ParamName);
        Assert.Equal("schemaContractCatalog", Assert.Throws<ArgumentNullException>(() => new VersionDetailsModel(
            orchestrationService,
            versionService,
            stageService,
            taskService,
            triggerService,
            null!,
            options)).ParamName);

        var nullOptionsPage = new VersionDetailsModel(
            orchestrationService,
            versionService,
            stageService,
            taskService,
            triggerService,
            catalog,
            null!);
        await nullOptionsPage.OnGetSchemaContractsAsync("Command", null!, 7, CancellationToken.None);

        await catalog.Received(1).SearchAsync(
            Arg.Is<SchemaContractCatalogSearchRequest>(request =>
                request.ProviderKey == "knowl" &&
                request.ContractKind == SchemaContractKind.Command &&
                request.SearchText == string.Empty &&
                request.Take == 7),
            Arg.Any<CancellationToken>());

        var nullValueOptions = Substitute.For<IOptions<OrchestratorDesignWebUIOptions>>();
        nullValueOptions.Value.Returns((OrchestratorDesignWebUIOptions)null!);
        var nullValuePage = new VersionDetailsModel(
            orchestrationService,
            versionService,
            stageService,
            taskService,
            triggerService,
            catalog,
            nullValueOptions);
        await nullValuePage.OnGetSchemaContractsAsync("Event", "sale", 3, CancellationToken.None);

        await catalog.Received(1).SearchAsync(
            Arg.Is<SchemaContractCatalogSearchRequest>(request =>
                request.ProviderKey == "knowl" &&
                request.ContractKind == SchemaContractKind.Event &&
                request.SearchText == "sale" &&
                request.Take == 3),
            Arg.Any<CancellationToken>());

        var customProviderPage = new VersionDetailsModel(
            orchestrationService,
            versionService,
            stageService,
            taskService,
            triggerService,
            catalog,
            Options.Create(new OrchestratorDesignWebUIOptions
            {
                DefaultSchemaRegistryProviderKey = " custom-provider "
            }));
        await customProviderPage.OnGetSchemaContractsAsync("Event", "sale", 9, CancellationToken.None);

        await catalog.Received(1).SearchAsync(
            Arg.Is<SchemaContractCatalogSearchRequest>(request =>
                request.ProviderKey == "custom-provider" &&
                request.ContractKind == SchemaContractKind.Event &&
                request.SearchText == "sale" &&
                request.Take == 9),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnGetReturnsNotFoundWhenVersionCannotBeLoaded()
    {
        var context = CreateContext();
        context.VersionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OrchestrationVersionModel>(null!));

        var result = await context.Page.OnGetAsync("orch-1", "missing");

        Assert.IsType<NotFoundResult>(result);
        Assert.False(context.Page.CanEdit);
    }

    [Fact]
    public async Task OnGetReturnsNotFoundWhenOrchestrationCannotBeLoaded()
    {
        var context = CreateContext();
        context.OrchestrationService.GetById(Arg.Any<GetOrchestrationDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OrchestrationDefinitionModel>(null!));

        var result = await context.Page.OnGetAsync("missing-orch", "version-1");

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
    public async Task TransitionVersionSurfacesFriendlyDslErrors()
    {
        var context = CreateContext();
        context.VersionService.Deploy(Arg.Any<DeployOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new OrchestrationArtifactDslValidationException(
                "stage:inventories:task:discountstock:transformation",
                "DSL has semantic errors."));

        var result = await context.Page.OnPostTransitionVersionAsync("orch-1", "version-1", "Deploy", CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains("cannot be published", context.Page.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("stage:inventories:task:discountstock:transformation", context.Page.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransitionVersionSurfacesInvalidOperationAndGenericErrors()
    {
        var invalidOperation = CreateContext();
        invalidOperation.VersionService.Deploy(Arg.Any<DeployOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("Runtime node credentials are not ready."));

        var invalidOperationResult = await invalidOperation.Page.OnPostTransitionVersionAsync(
            "orch-1",
            "version-1",
            "Deploy",
            CancellationToken.None);

        var generic = CreateContext();
        generic.VersionService.Approve(Arg.Any<ApproveOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new Exception("hidden"));

        var genericResult = await generic.Page.OnPostTransitionVersionAsync(
            "orch-1",
            "version-1",
            "Approve",
            CancellationToken.None);

        Assert.IsType<PageResult>(invalidOperationResult);
        Assert.Equal("Runtime node credentials are not ready.", invalidOperation.Page.ErrorMessage);
        Assert.IsType<PageResult>(genericResult);
        Assert.Equal("The operation could not be completed. Review the captured data and try again.", generic.Page.ErrorMessage);
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
    public async Task UpsertStageIgnoresTriggerModelStateErrors()
    {
        var context = CreateContext();
        context.Page.NewStage = new VersionDetailsModel.CreateStageInput
        {
            Key = "capture",
            Name = "Capture"
        };
        context.Page.ModelState.AddModelError("TriggerInput.Key", "The Key field is required.");
        context.Page.ModelState.AddModelError("TriggerInput.EventTopic", "The EventTopic field is required.");

        var result = await context.Page.OnPostUpsertStageAsync("orch-1", "version-1");

        Assert.IsType<RedirectToPageResult>(result);
        await context.StageService.Received(1).Create(
            Arg.Is<CreateStageDefinitionCommand>(command => command.Key == "capture"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StageEditorConditionDeleteAndReorderHandlersUseApplicationServices()
    {
        var context = CreateContext();

        Assert.IsType<BadRequestResult>(await context.Page.OnGetStageForEditAsync(""));
        Assert.IsType<JsonResult>(await context.Page.OnGetStageForEditAsync("stage-0"));
        Assert.IsType<RedirectToPageResult>(await context.Page.OnPostDeleteStageAsync("orch-1", "version-1", "stage-1"));
        Assert.IsType<BadRequestResult>(await context.Page.OnPostReorderStagesAsync(null!));
        Assert.IsType<JsonResult>(await context.Page.OnPostReorderStagesAsync(new VersionDetailsModel.ReorderStagesRequest
        {
            VersionId = "version-1",
            StageIds = ["stage-1", "stage-0", "missing"]
        }));

        await context.StageService.Received(1).Delete(Arg.Any<DeleteStageDefinitionCommand>(), Arg.Any<CancellationToken>());
        await context.StageService.Received().Update(Arg.Is<UpdateStageDefinitionCommand>(x => x.Id == "stage-1" && x.Order == 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StageEnabledToggleUsesStageLifecycleCommands()
    {
        var context = CreateContext();

        Assert.IsType<BadRequestResult>(await context.Page.OnPostSetStageEnabledAsync(null!));
        Assert.IsType<BadRequestResult>(await context.Page.OnPostSetStageEnabledAsync(new VersionDetailsModel.SetStageEnabledRequest()));
        Assert.IsType<JsonResult>(await context.Page.OnPostSetStageEnabledAsync(new VersionDetailsModel.SetStageEnabledRequest
        {
            StageId = "stage-1",
            IsEnabled = true
        }));
        Assert.IsType<JsonResult>(await context.Page.OnPostSetStageEnabledAsync(new VersionDetailsModel.SetStageEnabledRequest
        {
            StageId = "stage-0",
            IsEnabled = false
        }));

        await context.StageService.Received(1).Enable(
            Arg.Is<EnableStageDefinitionCommand>(command => command.Id == "stage-1"),
            Arg.Any<CancellationToken>());
        await context.StageService.Received(1).Disable(
            Arg.Is<DisableStageDefinitionCommand>(command => command.Id == "stage-0"),
            Arg.Any<CancellationToken>());
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

        var alreadyOrdered = CreateContext();
        alreadyOrdered.StageService.GetAll(Arg.Any<GetStageDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StageDefinitionModel>>(
            [
                CreateStage("stage-0", 0),
                CreateStage("stage-1", 1)
            ]));
        await InvokePrivateTaskAsync(alreadyOrdered.Page, "NormalizeStageOrderAsync", "version-1", CancellationToken.None);
        await alreadyOrdered.StageService.DidNotReceive().Update(
            Arg.Any<UpdateStageDefinitionCommand>(),
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
            EventTopic = "Events.Sales-Sale_Created",
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
    public async Task TriggerValidationCapturesOptionalDslCompensationAndProviderErrors()
    {
        var invalid = CreateContext();
        invalid.Page.TriggerInput = new VersionDetailsModel.UpsertTriggerInput
        {
            Key = "sales.sale.created",
            TriggerType = TriggerType.Event.ToString(),
            EventTopic = "",
            HasEventValidation = true,
            EventValidationDsl = "",
            HasCompensation = true,
            CompensationMessagingTopic = "",
            EventSchemaRegistryProviderId = "not-a-ulid"
        };

        var result = await invalid.Page.OnPostUpsertTriggerAsync("orch-1", "version-1");

        Assert.IsType<PageResult>(result);
        Assert.False(invalid.Page.ModelState.IsValid);
        Assert.True(invalid.Page.ModelState.ContainsKey(nameof(VersionDetailsModel.UpsertTriggerInput.EventTopic)));
        Assert.True(invalid.Page.ModelState.ContainsKey(nameof(VersionDetailsModel.UpsertTriggerInput.EventValidationDsl)));
        Assert.True(invalid.Page.ModelState.ContainsKey(nameof(VersionDetailsModel.UpsertTriggerInput.CompensationMessagingTopic)));
        Assert.True(invalid.Page.ModelState.ContainsKey(nameof(VersionDetailsModel.UpsertTriggerInput.EventSchemaRegistryProviderId)));
    }

    [Fact]
    public async Task TriggerHandlersCreateCompensationDefinitionWithConditionTransformationAndSchema()
    {
        var context = CreateContext();
        context.Page.TriggerInput = new VersionDetailsModel.UpsertTriggerInput
        {
            Key = "sales.sale.created",
            TriggerType = TriggerType.Event.ToString(),
            EventTopic = "events.sales.sale.created",
            EventVersion = "1.0.0",
            HasCompensation = true,
            CompensationMessagingTopic = "commands.sales.cancel",
            CompensationMessagingVersion = "1.0.0",
            HasCompensationSchemaValidation = true,
            CompensationSchemaContractKey = "commands.sales.cancel",
            CompensationSchemaContractVersion = "1.0.0",
            CompensationSchemaRegistryProviderId = "not-a-ulid",
            CompensationSchemaStrictMode = true,
            HasCompensationExecutionCondition = true,
            CompensationConditionDslExpression = "",
            HasCompensationTransformation = true,
            CompensationTransformationDsl = "map compensation"
        };

        var result = await context.Page.OnPostUpsertTriggerAsync("orch-1", "version-1", CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        await context.TriggerService.Received(1).Create(
            Arg.Is<CreateTriggerBindingCommand>(command => IsExpectedCompensatingTriggerCreateCommand(command)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertTriggerIgnoresStageModelStateErrors()
    {
        var context = CreateContext();
        context.Page.TriggerInput = new VersionDetailsModel.UpsertTriggerInput
        {
            Key = "sales.sale.created",
            TriggerType = TriggerType.Event.ToString(),
            EventTopic = "events.sales.sale.created",
            EventVersion = "1.0.0",
            EventSchemaContractKey = "events.sales.sale.created",
            EventSchemaContractVersion = "1.0.0"
        };
        context.Page.ModelState.AddModelError("NewStage.Key", "The Key field is required.");
        context.Page.ModelState.AddModelError("NewStage.Name", "The Name field is required.");

        var result = await context.Page.OnPostUpsertTriggerAsync("orch-1", "version-1");

        Assert.IsType<RedirectToPageResult>(result);
        await context.TriggerService.Received(1).Create(
            Arg.Is<CreateTriggerBindingCommand>(command => command.Key == "sales.sale.created"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SchemaContractsEndpointReturnsCatalogMatchesForTriggerAutocomplete()
    {
        var context = CreateContext();

        var result = await context.Page.OnGetSchemaContractsAsync("Event", "sale", cancellationToken: CancellationToken.None);

        var document = SerializeJsonResult(result);
        var item = document.RootElement.EnumerateArray().Single();
        Assert.Equal("sales.sale.created", item.GetProperty("contractKey").GetString());
        Assert.Equal("1.0.0", item.GetProperty("contractVersion").GetString());
        Assert.Equal("Event", item.GetProperty("contractKind").GetString());
        await context.SchemaContractCatalog.Received(1).SearchAsync(
            Arg.Is<SchemaContractCatalogSearchRequest>(request =>
                request.ProviderKey == "knowl" &&
                request.ContractKind == SchemaContractKind.Event &&
                request.SearchText == "sale"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SchemaContractsEndpointUsesFallbackDisplayNameAndNullSearchText()
    {
        var context = CreateContext();
        context.SchemaContractCatalog.SearchAsync(Arg.Any<SchemaContractCatalogSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<SchemaContractCatalogItem>>(
            [
                new SchemaContractCatalogItem
                {
                    ProviderKey = "knowl",
                    ContractId = "event-artifact",
                    ContractKey = "events.no.display",
                    ContractVersion = "2.0.0",
                    ContractKind = SchemaContractKind.Event,
                    ContentHash = "event-hash",
                    DisplayName = " "
                }
            ]));

        var result = await context.Page.OnGetSchemaContractsAsync(term: null!, cancellationToken: CancellationToken.None);

        var document = SerializeJsonResult(result);
        var item = document.RootElement.EnumerateArray().Single();
        Assert.Equal("events.no.display v2.0.0", item.GetProperty("displayName").GetString());
        await context.SchemaContractCatalog.Received(1).SearchAsync(
            Arg.Is<SchemaContractCatalogSearchRequest>(request =>
                request.ContractKind == SchemaContractKind.Event &&
                request.SearchText == string.Empty),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerHandlersUseFallbacksAndRedirectWhenEditedTriggerIsMissing()
    {
        var context = CreateContext();
        var triggerTypes = context.Page.TriggerTypes.ToArray();

        Assert.Equal(TriggerType.Event.ToString(), triggerTypes.Single().Value);

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
        context.TriggerService.GetById(
                Arg.Is<GetTriggerBindingByIdQuery>(query => query.Id == "missing-trigger"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((TriggerBindingModel)null!));

        var result = await context.Page.OnPostUpsertTriggerAsync("orch-1", "version-1", CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);

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
            " provider ",
            SchemaContractKind.Event);

        var eventChannel = Assert.IsType<EventTriggerChannel>(fallbackChannel);
        Assert.Equal("events.sales.sale.created", eventChannel.Topic);
        Assert.Equal("1.0.0", eventChannel.Version.ToString());
        Assert.Equal("contract", fallbackSchema.ContractKey);
        Assert.Equal("1.0.0", fallbackSchema.ContractVersion.ToString());
        Assert.Equal(" provider ", fallbackSchema.RegistryProviderKey);
    }

    [Fact]
    public void TriggerEditPayloadAndPrivateBuildersCoverNullAndDefaultBranches()
    {
        var emptyPayload = JsonSerializer.SerializeToDocument(InvokePrivateStatic<object>(
            "BuildTriggerEditPayload",
            new TriggerBindingModel
            {
                Id = "trigger-empty",
                OrchestrationVersionId = "version-1",
                Key = null!,
                TriggerType = TriggerType.Event,
                Description = null!,
                TriggerChannel = null!,
                CompensationDefinition = null
            }));
        var partialPayload = JsonSerializer.SerializeToDocument(InvokePrivateStatic<object>(
            "BuildTriggerEditPayload",
            new TriggerBindingModel
            {
                Id = "trigger-partial",
                OrchestrationVersionId = "version-1",
                Key = "trigger.partial",
                TriggerType = TriggerType.Event,
                Description = null!,
                TriggerChannel = new EventTriggerChannel
                {
                    Topic = null!,
                    Version = new SemanticVersion(0, 0, 0),
                    HasSchemaValidation = true,
                    SchemaBinding = new SchemaBinding
                    {
                        ElementType = ElementType.Orchestration,
                        ContractId = Id.New(),
                        ContractKey = "event.partial",
                        ContractVersion = new SemanticVersion(2, 0, 0),
                        RegistryProviderId = default,
                        RegistryProviderKey = "knowl",
                        StrictMode = false
                    },
                    HasValidation = true,
                    Validation = new ValidationDefinition
                    {
                        Engine = EngineType.DSL,
                        ErrorCode = null!,
                        Configuration = null!
                    }
                },
                CompensationDefinition = new CompensationDefinition
                {
                    CompensationTaskKind = TaskKind.Messaging,
                    DispatchType = TaskDispatchType.FireAndForget,
                    HasExecutionCondition = true,
                    ExecutionCondition = null!,
                    HasTransformation = true,
                    Transformation = null!,
                    Configuration = new MessagingTaskConfiguration
                    {
                        Topic = null,
                        Version = new SemanticVersion(0, 0, 0),
                        HasSchemaValidation = true,
                        SchemaBinding = new SchemaBinding
                        {
                            ElementType = ElementType.Orchestration,
                            ContractId = Id.New(),
                            ContractKey = "command.partial",
                            ContractVersion = new SemanticVersion(3, 0, 0),
                            RegistryProviderId = default,
                            RegistryProviderKey = "knowl",
                            StrictMode = true
                        }
                    }
                }
            }));
        var fullPayload = JsonSerializer.SerializeToDocument(InvokePrivateStatic<object>(
            "BuildTriggerEditPayload",
            new TriggerBindingModel
            {
                Id = "trigger-full",
                OrchestrationVersionId = "version-1",
                Key = "trigger.full",
                TriggerType = TriggerType.Event,
                Description = "Full trigger",
                TriggerChannel = new EventTriggerChannel
                {
                    Topic = "events.full",
                    Version = new SemanticVersion(4, 5, 6),
                    HasSchemaValidation = true,
                    SchemaBinding = new SchemaBinding
                    {
                        ElementType = ElementType.Orchestration,
                        ContractId = Id.New(),
                        ContractKey = "event.full",
                        ContractVersion = new SemanticVersion(4, 0, 0),
                        RegistryProviderId = Id.New(),
                        RegistryProviderKey = "knowl",
                        StrictMode = true
                    },
                    HasValidation = true,
                    Validation = new ValidationDefinition
                    {
                        Engine = EngineType.DSL,
                        ErrorCode = "EventRejected",
                        Configuration = new DslValidationConfiguration { Dsl = "$.id != null" }
                    }
                },
                CompensationDefinition = new CompensationDefinition
                {
                    CompensationTaskKind = TaskKind.Messaging,
                    DispatchType = TaskDispatchType.FireAndForget,
                    HasExecutionCondition = true,
                    ExecutionCondition = new ExecutionCondition
                    {
                        Engine = EngineType.DSL,
                        Configuration = new DslConditionConfiguration { Expression = new Expression("payload.ok") }
                    },
                    HasTransformation = true,
                    Transformation = new TransformationDefinition
                    {
                        Engine = EngineType.DSL,
                        Configuration = new DslTransformationConfiguration { Dsl = "map undo" }
                    },
                    Configuration = new MessagingTaskConfiguration
                    {
                        Topic = "commands.full.undo",
                        Version = new SemanticVersion(7, 8, 9),
                        HasSchemaValidation = true,
                        SchemaBinding = new SchemaBinding
                        {
                            ElementType = ElementType.Orchestration,
                            ContractId = Id.New(),
                            ContractKey = "command.full.undo",
                            ContractVersion = new SemanticVersion(7, 0, 0),
                            RegistryProviderId = Id.New(),
                            RegistryProviderKey = "knowl",
                            StrictMode = true
                        }
                    }
                }
            }));
        var disabledCompensation = InvokePrivateStatic<CompensationDefinition?>(
            "BuildTriggerCompensationDefinition",
            new VersionDetailsModel.UpsertTriggerInput { HasCompensation = false },
            "orch-1",
            "knowl");
        var compensationWithoutSchema = InvokePrivateStatic<CompensationDefinition?>(
            "BuildTriggerCompensationDefinition",
            new VersionDetailsModel.UpsertTriggerInput
            {
                HasCompensation = true,
                CompensationMessagingTopic = " commands.undo ",
                CompensationMessagingVersion = "bad",
                HasCompensationSchemaValidation = false,
                CompensationSchemaContractKey = "",
                HasCompensationExecutionCondition = true,
                CompensationConditionDslExpression = " ",
                HasCompensationTransformation = true,
                CompensationTransformationDsl = " "
            },
            "orch-1",
            "knowl");
        var compensationWithValidationOnlySchema = InvokePrivateStatic<CompensationDefinition?>(
            "BuildTriggerCompensationDefinition",
            new VersionDetailsModel.UpsertTriggerInput
            {
                HasCompensation = true,
                CompensationMessagingTopic = " commands.validated ",
                CompensationMessagingVersion = "3.2.1",
                HasCompensationSchemaValidation = true,
                CompensationSchemaContractKey = " commands.validated ",
                CompensationSchemaContractVersion = "3.2.1",
                CompensationSchemaStrictMode = true,
                HasCompensationExecutionCondition = false,
                HasCompensationTransformation = false
            },
            "orch-1",
            "knowl");
        var validation = InvokePrivateStatic<ValidationDefinition>("BuildValidation", null!, " ");
        var condition = InvokePrivateStatic<ExecutionCondition>("BuildExecutionCondition", " ");
        var resolvedTriggerType = InvokePrivateStatic<TriggerType>("ResolveTriggerType", (TriggerType)999);
        var emptyProvider = InvokePrivateStatic<string>("NormalizeProviderKey", (object?)null!);
        var trimmedProvider = InvokePrivateStatic<string>("NormalizeProviderKey", " custom ");
        var negativeVersion = InvokePrivateStatic<SemanticVersion>(
            "ParseSemanticVersion",
            "-1.2.-3",
            new SemanticVersion(9, 9, 9));

        Assert.Equal(string.Empty, emptyPayload.RootElement.GetProperty("Key").GetString());
        Assert.Equal(string.Empty, emptyPayload.RootElement.GetProperty("Description").GetString());
        Assert.Equal(string.Empty, emptyPayload.RootElement.GetProperty("EventTopic").GetString());
        Assert.Equal("1.0.0", emptyPayload.RootElement.GetProperty("EventVersion").GetString());
        Assert.False(emptyPayload.RootElement.GetProperty("HasEventSchemaValidation").GetBoolean());
        Assert.Equal(string.Empty, emptyPayload.RootElement.GetProperty("EventSchemaRegistryProviderId").GetString());
        Assert.Equal("TriggerValidationFailed", emptyPayload.RootElement.GetProperty("EventValidationErrorCode").GetString());
        Assert.False(emptyPayload.RootElement.GetProperty("HasCompensation").GetBoolean());
        Assert.Equal(string.Empty, emptyPayload.RootElement.GetProperty("CompensationMessagingTopic").GetString());
        Assert.Equal("1.0.0", emptyPayload.RootElement.GetProperty("CompensationMessagingVersion").GetString());
        Assert.Equal("event.partial", partialPayload.RootElement.GetProperty("EventSchemaContractKey").GetString());
        Assert.Equal(string.Empty, partialPayload.RootElement.GetProperty("EventSchemaRegistryProviderId").GetString());
        Assert.Equal(string.Empty, partialPayload.RootElement.GetProperty("EventValidationDsl").GetString());
        Assert.Equal("TriggerValidationFailed", partialPayload.RootElement.GetProperty("EventValidationErrorCode").GetString());
        Assert.Equal("command.partial", partialPayload.RootElement.GetProperty("CompensationSchemaContractKey").GetString());
        Assert.Equal(string.Empty, partialPayload.RootElement.GetProperty("CompensationSchemaRegistryProviderId").GetString());
        Assert.Equal("true", partialPayload.RootElement.GetProperty("CompensationConditionDslExpression").GetString());
        Assert.Equal(string.Empty, partialPayload.RootElement.GetProperty("CompensationTransformationDsl").GetString());
        Assert.NotEqual(string.Empty, fullPayload.RootElement.GetProperty("EventSchemaRegistryProviderId").GetString());
        Assert.Equal("$.id != null", fullPayload.RootElement.GetProperty("EventValidationDsl").GetString());
        Assert.Equal("EventRejected", fullPayload.RootElement.GetProperty("EventValidationErrorCode").GetString());
        Assert.NotEqual(string.Empty, fullPayload.RootElement.GetProperty("CompensationSchemaRegistryProviderId").GetString());
        Assert.Equal("payload.ok", fullPayload.RootElement.GetProperty("CompensationConditionDslExpression").GetString());
        Assert.Equal("map undo", fullPayload.RootElement.GetProperty("CompensationTransformationDsl").GetString());
        Assert.Null(disabledCompensation);
        Assert.NotNull(compensationWithoutSchema);
        var messaging = Assert.IsType<MessagingTaskConfiguration>(compensationWithoutSchema!.Configuration);
        Assert.Equal("commands.undo", messaging.Topic);
        Assert.Equal(new SemanticVersion(1, 0, 0), messaging.Version);
        Assert.Null(messaging.SchemaBinding);
        var validationOnlyMessaging = Assert.IsType<MessagingTaskConfiguration>(compensationWithValidationOnlySchema!.Configuration);
        Assert.NotNull(validationOnlyMessaging.SchemaBinding);
        Assert.Equal("commands.validated", validationOnlyMessaging.SchemaBinding!.ContractKey);
        Assert.True(validationOnlyMessaging.SchemaBinding.StrictMode);
        Assert.True(validationOnlyMessaging.SchemaBinding.IsValidationEnabled);
        Assert.True(compensationWithoutSchema.HasExecutionCondition);
        Assert.Equal("true", ((DslConditionConfiguration)compensationWithoutSchema.ExecutionCondition!.Configuration).Expression.ToString());
        Assert.True(compensationWithoutSchema.HasTransformation);
        Assert.Null(compensationWithoutSchema.Transformation);
        Assert.Equal("TriggerValidationFailed", validation.ErrorCode);
        Assert.Equal(string.Empty, ((DslValidationConfiguration)validation.Configuration).Dsl);
        Assert.Equal("true", ((DslConditionConfiguration)condition.Configuration).Expression.ToString());
        Assert.Equal(TriggerType.Event, resolvedTriggerType);
        Assert.Equal("knowl", emptyProvider);
        Assert.Equal("custom", trimmedProvider);
        Assert.Equal(new SemanticVersion(0, 2, 0), negativeVersion);
    }

    [Fact]
    public void AllowedActionsFollowVersionLifecycle()
    {
        var page = CreateContext().Page;

        Assert.Equal(["SetInReview", "Archive"], page.GetAllowedActions(OrchestrationVersionStatus.Draft));
        Assert.Equal(["Approve", "ReturnToDraft", "Archive"], page.GetAllowedActions(OrchestrationVersionStatus.InReview));
        Assert.Equal(["Deploy", "ReopenReview", "Archive"], page.GetAllowedActions(OrchestrationVersionStatus.Approved));
        Assert.Equal(["Deprecate", "Archive"], page.GetAllowedActions(OrchestrationVersionStatus.Deployed));
        Assert.Equal(["Archive"], page.GetAllowedActions(OrchestrationVersionStatus.Deprecated));
        Assert.Empty(page.GetAllowedActions(OrchestrationVersionStatus.Archived));
    }

    [Fact]
    public void PrivateFallbackHelpersHandleModelLevelErrorsAndSemanticVersionEdges()
    {
        var page = CreateContext().Page;
        InvokePrivateGenericVoid(page, "ValidateInputModel", typeof(ModelLevelInvalidInput), new ModelLevelInvalidInput(), "ModelLevel");

        var blankVersion = InvokePrivateStatic<SemanticVersion>(
            "ParseSemanticVersion",
            "",
            new SemanticVersion(9, 9, 9));
        var invalidVersion = InvokePrivateStatic<SemanticVersion>(
            "ParseSemanticVersion",
            "x.y.z",
            new SemanticVersion(8, 8, 8));
        var blankTransformation = InvokePrivateStatic<TransformationDefinition?>("BuildTransformation", " ");
        var mappedTransformation = InvokePrivateStatic<TransformationDefinition?>("BuildTransformation", "map payload");

        Assert.True(page.ModelState.ContainsKey("ModelLevel"));
        Assert.Equal("9.9.9", blankVersion.ToString());
        Assert.Equal("8.8.8", invalidVersion.ToString());
        Assert.Null(blankTransformation);
        Assert.NotNull(mappedTransformation);
        Assert.IsType<DslTransformationConfiguration>(mappedTransformation!.Configuration);
    }

    [Fact]
    public void PrivateValidationHelperMapsNamedAndFallbackValidationMembers()
    {
        var page = CreateContext().Page;

        InvokePrivateGenericVoid(page, "ValidateInputModel", typeof(MixedMemberInvalidInput), new MixedMemberInvalidInput(), "MixedModel");
        InvokePrivateGenericVoid(page, "ValidateInputModel", typeof(NullMemberInvalidInput), new NullMemberInvalidInput(), "NullMemberModel");

        Assert.True(page.ModelState.ContainsKey("MixedModel.Key"));
        Assert.True(page.ModelState.ContainsKey("MixedModel"));
        Assert.True(page.ModelState.ContainsKey("NullMemberModel"));
        Assert.True(page.ModelState.ContainsKey("NullMemberModel.Key"));
    }

    [Fact]
    public void PrivateTriggerValidationAndPayloadHelpersCoverRemainingBranches()
    {
        var page = CreateContext().Page;
        page.TriggerInput = new VersionDetailsModel.UpsertTriggerInput
        {
            TriggerType = TriggerType.Event.ToString(),
            EventTopic = " ",
            HasEventValidation = true,
            EventValidationDsl = " ",
            HasCompensation = true,
            CompensationMessagingTopic = " ",
            EventSchemaRegistryProviderId = "not-a-ulid"
        };

        InvokePrivate(page, "ValidateTriggerInput");

        var payload = JsonSerializer.SerializeToDocument(InvokePrivateStatic<object>(
            "BuildTriggerEditPayload",
            new TriggerBindingModel
            {
                Id = "trigger-human-compensation",
                Key = null!,
                Description = null!,
                TriggerType = TriggerType.Event,
                TriggerChannel = new EventTriggerChannel
                {
                    Topic = null!,
                    SchemaBinding = new SchemaBinding
                    {
                        ElementType = ElementType.Orchestration,
                        ContractId = Id.New(),
                        ContractKey = null!,
                        RegistryProviderId = default
                    }
                },
                CompensationDefinition = new CompensationDefinition
                {
                    CompensationTaskKind = TaskKind.HumanApproval,
                    Configuration = new HumanApprovalTaskConfiguration(),
                    ExecutionCondition = new ExecutionCondition
                    {
                        Engine = EngineType.DSL,
                        Configuration = null!
                    },
                    Transformation = new TransformationDefinition
                    {
                        Engine = EngineType.DSL,
                        Configuration = null!
                    }
                }
            }));

        Assert.True(page.ModelState.ContainsKey(nameof(VersionDetailsModel.UpsertTriggerInput.EventTopic)));
        Assert.True(page.ModelState.ContainsKey(nameof(VersionDetailsModel.UpsertTriggerInput.EventValidationDsl)));
        Assert.True(page.ModelState.ContainsKey(nameof(VersionDetailsModel.UpsertTriggerInput.CompensationMessagingTopic)));
        Assert.True(page.ModelState.ContainsKey(nameof(VersionDetailsModel.UpsertTriggerInput.EventSchemaRegistryProviderId)));
        Assert.Equal(string.Empty, payload.RootElement.GetProperty("Key").GetString());
        Assert.Equal(string.Empty, payload.RootElement.GetProperty("EventTopic").GetString());
        Assert.Equal(string.Empty, payload.RootElement.GetProperty("CompensationMessagingTopic").GetString());
    }

    private static TestContext CreateContext()
    {
        var orchestrationService = Substitute.For<IOrchestrationApplicationService>();
        var versionService = Substitute.For<IOrchestrationVersionApplicationService>();
        var stageService = Substitute.For<IStageApplicationService>();
        var taskService = Substitute.For<ITaskApplicationService>();
        var triggerService = Substitute.For<ITriggerBindingApplicationService>();
        var schemaContractCatalog = Substitute.For<ISchemaContractCatalog>();
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
        stageService.Enable(Arg.Any<EnableStageDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        stageService.Disable(Arg.Any<DisableStageDefinitionCommand>(), Arg.Any<CancellationToken>())
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
        schemaContractCatalog.SearchAsync(Arg.Any<SchemaContractCatalogSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<SchemaContractCatalogItem>>(
            [
                new SchemaContractCatalogItem
                {
                    ProviderKey = "knowl",
                    ContractId = "event-artifact",
                    ContractKey = "sales.sale.created",
                    ContractVersion = "1.0.0",
                    ContractKind = SchemaContractKind.Event,
                    ContentHash = "event-hash",
                    DisplayName = "sales.sale.created v1.0.0"
                }
            ]));

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
            schemaContractCatalog,
            Options.Create(new OrchestratorDesignWebUIOptions { DefaultSchemaRegistryProviderKey = "knowl" }));

        return new TestContext(page, orchestrationService, versionService, stageService, triggerService, schemaContractCatalog);
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
               channel.Topic == "Events.Sales-Sale_Created" &&
               channel.Version.ToString() == "1.1.0" &&
               channel.SchemaBinding is not null &&
               channel.SchemaBinding.RegistryProviderKey == "knowl";
    }

    private static bool IsExpectedCompensatingTriggerCreateCommand(CreateTriggerBindingCommand command)
    {
        var compensation = command.CompensationDefinition;
        var configuration = compensation?.Configuration as MessagingTaskConfiguration;
        var condition = compensation?.ExecutionCondition?.Configuration as DslConditionConfiguration;
        var transformation = compensation?.Transformation?.Configuration as DslTransformationConfiguration;

        return compensation is not null &&
               compensation.CompensationTaskKind == TaskKind.Messaging &&
               compensation.DispatchType == TaskDispatchType.FireAndForget &&
               configuration is not null &&
               configuration.Topic == "commands.sales.cancel" &&
               configuration.Version.ToString() == "1.0.0" &&
               configuration.HasSchemaValidation &&
               configuration.SchemaBinding is not null &&
               configuration.SchemaBinding.ContractKey == "commands.sales.cancel" &&
               configuration.SchemaBinding.ContractVersion.ToString() == "1.0.0" &&
               configuration.SchemaBinding.ContractKind == SchemaContractKind.Command &&
               configuration.SchemaBinding.StrictMode &&
               compensation.HasExecutionCondition &&
               condition is not null &&
               condition.Expression.ToString() == "true" &&
               compensation.HasTransformation &&
               transformation is not null &&
               transformation.Dsl == "map compensation";
    }

    private static Task InvokePrivateTaskAsync(VersionDetailsModel model, string methodName, params object?[] args)
    {
        var method = typeof(VersionDetailsModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(model, args)!;
    }

    private static void InvokePrivateGenericVoid(VersionDetailsModel model, string methodName, Type genericType, params object?[] args)
    {
        var method = typeof(VersionDetailsModel)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(genericType);
        method.Invoke(model, args);
    }

    private static void InvokePrivate(VersionDetailsModel model, string methodName, params object?[] args)
    {
        var method = typeof(VersionDetailsModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(model, args);
    }

    private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
    {
        var method = typeof(VersionDetailsModel).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        return (T)method.Invoke(null, args)!;
    }

    private sealed record TestContext(
        VersionDetailsModel Page,
        IOrchestrationApplicationService OrchestrationService,
        IOrchestrationVersionApplicationService VersionService,
        IStageApplicationService StageService,
        ITriggerBindingApplicationService TriggerService,
        ISchemaContractCatalog SchemaContractCatalog);

    private sealed class ModelLevelInvalidInput : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            yield return new ValidationResult("Model-level validation failed.");
        }
    }

    private sealed class MixedMemberInvalidInput : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            yield return new ValidationResult("Named validation failed.", ["", "Key"]);
            yield return new ValidationResult(null, []);
        }
    }

    private sealed class NullMemberInvalidInput : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            yield return new ValidationResult("Null member validation failed.", null);
            yield return new ValidationResult(null, ["Key"]);
        }
    }

    private static JsonDocument SerializeJsonResult(IActionResult result)
    {
        var json = Assert.IsType<JsonResult>(result);
        return System.Text.Json.JsonSerializer.SerializeToDocument(json.Value);
    }
}
