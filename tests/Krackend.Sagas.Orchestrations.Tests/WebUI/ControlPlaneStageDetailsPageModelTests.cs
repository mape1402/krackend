namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.OrchestrationStages;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Reflection;
using System.Text.Json;

public sealed class ControlPlaneStageDetailsPageModelTests
{
    [Fact]
    public async Task OnGetLoadsStageDataAndOpensEditModalForDraftVersion()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        var task = TaskModel(stageId, "inventories.reserve", 0);
        fixture.StageService.GetById(Arg.Any<GetStageDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(StageModel(stageId, "inventory-reservation"));
        fixture.VersionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(VersionModel("version-1", OrchestrationVersionStatus.Draft));
        fixture.TaskService.GetAll(Arg.Any<GetTaskDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([task]);
        fixture.TaskService.GetById(Arg.Any<GetTaskDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(task);
        fixture.ParallelGroupService.GetAll(Arg.Any<GetParallelGroupDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var model = fixture.CreateModel();

        var result = await model.OnGetAsync("orch-1", "version-1", stageId, task.Id, CancellationToken.None);
        var redirect = await model.OnGetAsync(string.Empty, "version-1", stageId, cancellationToken: CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(model.CanEdit);
        Assert.True(model.ShouldOpenEditModal);
        Assert.Contains(task.Id, model.EditTaskPayloadJson, StringComparison.Ordinal);
        Assert.Single(model.Tasks);
        Assert.IsType<RedirectToPageResult>(redirect);
    }

    [Fact]
    public async Task SelectListsExposeSupportedConfigurationOptions()
    {
        var model = new Fixture().CreateModel();

        Assert.Equal([TaskKind.Messaging.ToString()], model.TaskKinds.Select(x => x.Value).ToArray());
        Assert.Contains(TaskExecutionMode.Sequential.ToString(), model.ExecutionModes.Select(x => x.Value));
        Assert.Contains(TaskDispatchType.FireAndWaitCallback.ToString(), model.DispatchTypes.Select(x => x.Value));
        Assert.Equal([TaskDispatchType.FireAndForget.ToString()], model.CompensationDispatchTypes.Select(x => x.Value).ToArray());
        Assert.Contains(OnErrorPolicy.StopAndCompensate.ToString(), model.OnErrorPolicies.Select(x => x.Value));
        Assert.Equal([EngineType.DSL.ToString()], model.EngineTypes.Select(x => x.Value).ToArray());
        Assert.Equal([RetryStrategyType.Fixed.ToString()], model.RetryStrategyTypes.Select(x => x.Value).ToArray());
        Assert.Contains(TimeoutBehavior.Reconcile.ToString(), model.TimeoutBehaviors.Select(x => x.Value));
        Assert.Contains(OrchestrationActionOnTimeout.Continue.ToString(), model.TimeoutActions.Select(x => x.Value));
    }

    [Fact]
    public async Task OnGetReturnsNotFoundAndStoresErrorWhenLoadingStageFails()
    {
        var fixture = new Fixture();
        fixture.StageService.GetById(Arg.Any<GetStageDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StageDefinitionModel>(new InvalidOperationException("stage storage failed")));
        var model = fixture.CreateModel();

        var result = await model.OnGetAsync("orch-1", "version-1", "stage-1", cancellationToken: CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal("stage storage failed", model.ErrorMessage);
    }

    [Fact]
    public async Task OnGetDoesNotOpenEditModalForReadOnlyVersion()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        var task = TaskModel(stageId, "inventories.reserve", 0);
        fixture.StageService.GetById(Arg.Any<GetStageDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(StageModel(stageId, "inventory-reservation"));
        fixture.VersionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(VersionModel("version-1", OrchestrationVersionStatus.Deployed));
        fixture.TaskService.GetAll(Arg.Any<GetTaskDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([task]);
        fixture.TaskService.GetById(Arg.Any<GetTaskDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(task);
        fixture.ParallelGroupService.GetAll(Arg.Any<GetParallelGroupDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var model = fixture.CreateModel();

        var result = await model.OnGetAsync("orch-1", "version-1", stageId, task.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(model.CanEdit);
        Assert.False(model.ShouldOpenEditModal);
        await fixture.TaskService.DidNotReceive().GetById(Arg.Any<GetTaskDefinitionByIdQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostSetTaskEnabledTogglesTaskState()
    {
        var fixture = new Fixture();
        fixture.TaskService.Enable(Arg.Any<EnableTaskDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        fixture.TaskService.Disable(Arg.Any<DisableTaskDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var model = fixture.CreateModel();

        var enabledResult = await model.OnPostSetTaskEnabledAsync(
            new DetailsModel.SetTaskEnabledRequest { TaskId = "task-1", IsEnabled = true },
            CancellationToken.None);
        var disabledResult = await model.OnPostSetTaskEnabledAsync(
            new DetailsModel.SetTaskEnabledRequest { TaskId = "task-2", IsEnabled = false },
            CancellationToken.None);

        AssertJsonBoolean(enabledResult, "success", true);
        AssertJsonBoolean(disabledResult, "success", false);
        await fixture.TaskService.Received(1).Enable(
            Arg.Is<EnableTaskDefinitionCommand>(command => command.Id == "task-1"),
            Arg.Any<CancellationToken>());
        await fixture.TaskService.Received(1).Disable(
            Arg.Is<DisableTaskDefinitionCommand>(command => command.Id == "task-2"),
            Arg.Any<CancellationToken>());
        Assert.IsType<BadRequestResult>(await model.OnPostSetTaskEnabledAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task OnPostReorderTasksUpdatesOrderAndParallelAssignment()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        var parallelGroupId = Id.New().ToString();
        var first = TaskModel(stageId, "task.first", 10);
        var second = TaskModel(stageId, "task.second", 20);
        fixture.TaskService.GetAll(Arg.Any<GetTaskDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([first, second]);
        fixture.TaskService.Update(Arg.Any<UpdateTaskDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var model = fixture.CreateModel();

        var result = await model.OnPostReorderTasksAsync(
            new DetailsModel.ReorderTasksRequest
            {
                StageId = stageId,
                Items =
                [
                    new DetailsModel.ReorderTaskPlacementRequest { TaskId = second.Id, ParallelGroupId = parallelGroupId },
                    new DetailsModel.ReorderTaskPlacementRequest { TaskId = first.Id, ParallelGroupId = "not-a-ulid" },
                    new DetailsModel.ReorderTaskPlacementRequest { TaskId = second.Id, ParallelGroupId = parallelGroupId }
                ]
            },
            CancellationToken.None);

        AssertJsonBoolean(result, "success", true);
        await fixture.TaskService.Received(1).Update(
            Arg.Is<UpdateTaskDefinitionCommand>(command =>
                command.Id == second.Id &&
                command.Order == 0 &&
                command.ExecutionMode == TaskExecutionMode.Parallel &&
                command.ParallelGroupId == parallelGroupId),
            Arg.Any<CancellationToken>());
        await fixture.TaskService.Received(1).Update(
            Arg.Is<UpdateTaskDefinitionCommand>(command =>
                command.Id == first.Id &&
                command.Order == 1 &&
                command.ExecutionMode == TaskExecutionMode.Sequential &&
                command.ParallelGroupId == string.Empty),
            Arg.Any<CancellationToken>());
        Assert.IsType<BadRequestResult>(await model.OnPostReorderTasksAsync(
            new DetailsModel.ReorderTasksRequest { StageId = stageId },
            CancellationToken.None));
    }

    [Fact]
    public async Task TaskEditEndpointsReturnConditionTransformationAndFullTaskPayloads()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        var task = TaskModel(stageId, "payments.capture", 0);
        task.HasExecutionCondition = true;
        task.ExecutionCondition = DslCondition("$trigger.total > 0");
        task.HasTransformation = true;
        task.Transformation = DslTransformation("map payment request", "ctx-hash", "target-hash");
        task.RetryPolicy = new RetryPolicy
        {
            MaxRetries = 2,
            StrategyType = RetryStrategyType.Fixed,
            Strategy = new FixedRetryStrategy { Delay = Duration.FromSeconds(7) },
            RetryableErrorCodes = ["TEMP"],
            StopOnNonRetryableError = true
        };
        task.TimeoutPolicy = new TimeoutPolicy
        {
            Timeout = Duration.FromSeconds(30),
            TimeoutBehavior = TimeoutBehavior.Reconcile,
            TimeoutBehaviorPolicy = new ReconcileTimeoutBehaviorPolicy
            {
                OrchestrationAction = OrchestrationActionOnTimeout.Block,
                RetryPolicy = FixedRetryPolicy(3, 9, ["PAYMENT_TIMEOUT"], false)
            }
        };
        fixture.TaskService.GetById(Arg.Any<GetTaskDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(task);
        var model = fixture.CreateModel();

        var editResult = await model.OnGetTaskForEditAsync(task.Id, CancellationToken.None);
        var conditionResult = await model.OnGetTaskExecutionConditionForEditAsync(task.Id, CancellationToken.None);
        var transformationResult = await model.OnGetTaskTransformationForEditAsync(task.Id, CancellationToken.None);

        AssertJsonString(editResult, "TaskId", task.Id);
        AssertJsonString(editResult, "MessagingTopic", "payments.capture");
        AssertJsonString(conditionResult, "conditionDslExpression", "$trigger.total > 0");
        AssertJsonString(transformationResult, "transformationDsl", "map payment request");
        AssertJsonString(transformationResult, "sourceContextHash", "ctx-hash");
        var editPayload = SerializeJsonResult(editResult);
        Assert.Equal(3, editPayload.RootElement.GetProperty("TimeoutReconcileRetries").GetInt32());
        Assert.Equal(9d, editPayload.RootElement.GetProperty("TimeoutReconcileDelaySeconds").GetDouble());
        Assert.Equal("PAYMENT_TIMEOUT", editPayload.RootElement.GetProperty("TimeoutReconcileRetryableErrorCodes").GetString());
        Assert.IsType<BadRequestResult>(await model.OnGetTaskForEditAsync(string.Empty, CancellationToken.None));
        Assert.IsType<BadRequestResult>(await model.OnGetTaskExecutionConditionForEditAsync(string.Empty, CancellationToken.None));
        Assert.IsType<BadRequestResult>(await model.OnGetTaskTransformationForEditAsync(string.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task TaskEditPayloadIncludesHttpPluginAndCompensationConfiguration()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        var httpTask = TaskModel(stageId, "http.reserve", 0);
        httpTask.Kind = TaskKind.Http;
        httpTask.Configuration = new HttpTaskConfiguration
        {
            BaseUrlVariableRef = "inventory-api",
            RelativePath = "/reserve",
            Method = "post",
            ExpectedStatusCodes = [200, 202],
            AllowSyncResponse = true,
            HasSchemaValidation = true,
            SchemaBinding = SchemaBinding("ReserveInventoryHttpRequest", SchemaContractKind.CommandRequest)
        };
        httpTask.RetryPolicy = FixedRetryPolicy(4, 6, ["TEMP", "LOCKED"], true);
        httpTask.TimeoutPolicy = new TimeoutPolicy
        {
            Timeout = Duration.FromSeconds(12),
            TimeoutBehavior = TimeoutBehavior.Fail,
            TimeoutBehaviorPolicy = new FailTimeoutBehaviorPolicy { ErrorCode = null }
        };
        httpTask.CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Http,
            DispatchType = TaskDispatchType.FireAndForget,
            HasExecutionCondition = true,
            ExecutionCondition = DslCondition("$task.reserved == true"),
            HasTransformation = true,
            Transformation = DslTransformation("map release"),
            Configuration = new HttpTaskConfiguration
            {
                BaseUrlVariableRef = "inventory-api",
                RelativePath = "/release",
                Method = "delete",
                ExpectedStatusCodes = null,
                AllowSyncResponse = false,
                HasSchemaValidation = true,
                SchemaBinding = SchemaBinding("ReleaseInventoryHttpRequest", SchemaContractKind.CommandRequest)
            },
            RetryPolicy = FixedRetryPolicy(1, 3, ["UNDO_TEMP"], false),
            TimeoutPolicy = new TimeoutPolicy
            {
                Timeout = Duration.FromSeconds(5),
                TimeoutBehavior = TimeoutBehavior.Wait,
                TimeoutBehaviorPolicy = new WaitTimeoutBehaviorPolicy
                {
                    OrchestrationAction = OrchestrationActionOnTimeout.Continue,
                    WaitingTime = Duration.FromSeconds(2)
                }
            }
        };

        var pluginId = Id.New();
        var compensationPluginId = Id.New();
        var pluginTask = TaskModel(stageId, "plugin.credit", 1);
        pluginTask.Kind = TaskKind.Plugin;
        pluginTask.Configuration = new PluginTaskConfiguration { PluginId = pluginId };
        pluginTask.CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Plugin,
            DispatchType = TaskDispatchType.FireAndForget,
            Configuration = new PluginTaskConfiguration { PluginId = compensationPluginId },
            TimeoutPolicy = new TimeoutPolicy
            {
                Timeout = Duration.FromSeconds(20),
                TimeoutBehavior = TimeoutBehavior.Reconcile,
                TimeoutBehaviorPolicy = new ReconcileTimeoutBehaviorPolicy
                {
                    OrchestrationAction = OrchestrationActionOnTimeout.Block,
                    RetryPolicy = FixedRetryPolicy(2, 8, ["TIMEOUT"], true)
                }
            }
        };
        fixture.TaskService.GetById(
                Arg.Is<GetTaskDefinitionByIdQuery>(query => query.Id == httpTask.Id),
                Arg.Any<CancellationToken>())
            .Returns(httpTask);
        fixture.TaskService.GetById(
                Arg.Is<GetTaskDefinitionByIdQuery>(query => query.Id == pluginTask.Id),
                Arg.Any<CancellationToken>())
            .Returns(pluginTask);
        var model = fixture.CreateModel();

        var httpPayload = SerializeJsonResult(await model.OnGetTaskForEditAsync(httpTask.Id, CancellationToken.None));
        var pluginPayload = SerializeJsonResult(await model.OnGetTaskForEditAsync(pluginTask.Id, CancellationToken.None));

        Assert.Equal("inventory-api", httpPayload.RootElement.GetProperty("HttpBaseUrlVariableRef").GetString());
        Assert.Equal("/reserve", httpPayload.RootElement.GetProperty("HttpRelativePath").GetString());
        Assert.Equal("post", httpPayload.RootElement.GetProperty("HttpMethod").GetString());
        Assert.Equal("200,202", httpPayload.RootElement.GetProperty("HttpExpectedStatusCodes").GetString());
        Assert.True(httpPayload.RootElement.GetProperty("HttpAllowSyncResponse").GetBoolean());
        Assert.Equal("ReserveInventoryHttpRequest", httpPayload.RootElement.GetProperty("HttpSchemaContractKey").GetString());
        Assert.Equal(4, httpPayload.RootElement.GetProperty("RetryMaxRetries").GetInt32());
        Assert.Equal("TIMEOUT", httpPayload.RootElement.GetProperty("TimeoutFailErrorCode").GetString());
        Assert.Equal("inventory-api", httpPayload.RootElement.GetProperty("CompensationHttpBaseUrlVariableRef").GetString());
        Assert.Equal("200", httpPayload.RootElement.GetProperty("CompensationHttpExpectedStatusCodes").GetString());
        Assert.Equal("ReleaseInventoryHttpRequest", httpPayload.RootElement.GetProperty("CompensationHttpSchemaContractKey").GetString());
        Assert.Equal(1, httpPayload.RootElement.GetProperty("CompensationRetryMaxRetries").GetInt32());
        Assert.Equal(2d, httpPayload.RootElement.GetProperty("CompensationTimeoutWaitSeconds").GetDouble());

        Assert.Equal(pluginId.ToString(), pluginPayload.RootElement.GetProperty("PluginId").GetString());
        Assert.Equal(compensationPluginId.ToString(), pluginPayload.RootElement.GetProperty("CompensationPluginId").GetString());
        Assert.Equal(2, pluginPayload.RootElement.GetProperty("CompensationTimeoutReconcileRetries").GetInt32());
        Assert.Equal("TIMEOUT", pluginPayload.RootElement.GetProperty("CompensationTimeoutReconcileRetryableErrorCodes").GetString());
        Assert.True(pluginPayload.RootElement.GetProperty("CompensationTimeoutReconcileStopOnNonRetryableError").GetBoolean());
    }

    [Fact]
    public async Task TaskEditPayloadIncludesMessagingCompensationConfiguration()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        var task = TaskModel(stageId, "inventories.reserve", 0);
        task.CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Messaging,
            DispatchType = TaskDispatchType.FireAndForget,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "inventories.release",
                Version = new SemanticVersion(2, 0, 0),
                HasSchemaValidation = true,
                SchemaBinding = SchemaBinding("ReleaseInventory", SchemaContractKind.CommandRequest),
                HasRequestValidation = true,
                RequestValidation = new ValidationDefinition
                {
                    Engine = EngineType.DSL,
                    ErrorCode = "InvalidReleaseRequest",
                    Configuration = new DslValidationConfiguration { Dsl = "$.saleId != null" }
                },
                HasResponseValidation = true,
                ResponseValidation = new ValidationDefinition
                {
                    Engine = EngineType.DSL,
                    ErrorCode = "InvalidReleaseResponse",
                    Configuration = new DslValidationConfiguration { Dsl = "$.released == true" }
                }
            },
            TimeoutPolicy = new TimeoutPolicy
            {
                Timeout = Duration.FromSeconds(10),
                TimeoutBehavior = TimeoutBehavior.Fail,
                TimeoutBehaviorPolicy = new FailTimeoutBehaviorPolicy { ErrorCode = "ReleaseTimeout" }
            }
        };
        fixture.TaskService.GetById(Arg.Any<GetTaskDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(task);
        var model = fixture.CreateModel();

        var payload = SerializeJsonResult(await model.OnGetTaskForEditAsync(task.Id, CancellationToken.None));

        Assert.Equal("inventories.release", payload.RootElement.GetProperty("CompensationMessagingTopic").GetString());
        Assert.Equal("2.0.0", payload.RootElement.GetProperty("CompensationMessagingVersion").GetString());
        Assert.True(payload.RootElement.GetProperty("HasCompensationMessagingSchemaValidation").GetBoolean());
        Assert.Equal("ReleaseInventory", payload.RootElement.GetProperty("CompensationMessagingSchemaContractKey").GetString());
        Assert.True(payload.RootElement.GetProperty("HasCompensationMessagingRequestValidation").GetBoolean());
        Assert.Equal("$.saleId != null", payload.RootElement.GetProperty("CompensationMessagingRequestValidationDsl").GetString());
        Assert.Equal("InvalidReleaseRequest", payload.RootElement.GetProperty("CompensationMessagingRequestValidationErrorCode").GetString());
        Assert.True(payload.RootElement.GetProperty("HasCompensationMessagingResponseValidation").GetBoolean());
        Assert.Equal("$.released == true", payload.RootElement.GetProperty("CompensationMessagingResponseValidationDsl").GetString());
        Assert.Equal("InvalidReleaseResponse", payload.RootElement.GetProperty("CompensationMessagingResponseValidationErrorCode").GetString());
        Assert.Equal("ReleaseTimeout", payload.RootElement.GetProperty("CompensationTimeoutFailErrorCode").GetString());
    }

    [Fact]
    public void PrivateValidationHelpersValidateHttpAndPluginConfiguration()
    {
        var model = new Fixture().CreateModel();
        model.NewTask = new DetailsModel.CreateTaskInput
        {
            HasHttpSchemaValidation = true,
            HttpExpectedStatusCodes = "bad,0",
            HttpSchemaRegistryProviderId = "not-a-ulid",
            HasCompensationHttpSchemaValidation = true,
            CompensationHttpExpectedStatusCodes = "",
            CompensationHttpSchemaRegistryProviderId = "not-a-ulid",
            PluginId = "not-a-ulid",
            CompensationPluginId = ""
        };

        InvokePrivate(model, "ValidateTaskConfiguration", TaskKind.Http, string.Empty);
        InvokePrivate(model, "ValidateTaskConfiguration", TaskKind.Http, "Compensation");
        InvokePrivate(model, "ValidateTaskConfiguration", TaskKind.Plugin, string.Empty);
        InvokePrivate(model, "ValidateTaskConfiguration", TaskKind.Plugin, "Compensation");

        Assert.Contains("HttpBaseUrlVariableRef", model.ModelState.Keys);
        Assert.Contains("HttpRelativePath", model.ModelState.Keys);
        Assert.Contains("HttpExpectedStatusCodes", model.ModelState.Keys);
        Assert.Contains("HttpSchemaContractKey", model.ModelState.Keys);
        Assert.Contains("HttpSchemaRegistryProviderId", model.ModelState.Keys);
        Assert.Contains("CompensationHttpBaseUrlVariableRef", model.ModelState.Keys);
        Assert.Contains("CompensationHttpRelativePath", model.ModelState.Keys);
        Assert.Contains("CompensationHttpExpectedStatusCodes", model.ModelState.Keys);
        Assert.Contains("CompensationHttpSchemaContractKey", model.ModelState.Keys);
        Assert.Contains("CompensationPluginId", model.ModelState.Keys);
        Assert.Contains("PluginId", model.ModelState.Keys);
    }

    [Fact]
    public void PrivateBuilderHelpersBuildNonMessagingConfigurationsAndFallbacks()
    {
        var pluginId = Id.New().ToString();
        var compensationPluginId = Id.New().ToString();
        var input = new DetailsModel.CreateTaskInput
        {
            HttpBaseUrlVariableRef = " service.url ",
            HttpRelativePath = " /reserve ",
            HttpMethod = "post",
            HttpExpectedStatusCodes = "202,200,abc,202",
            HttpAllowSyncResponse = true,
            HasHttpSchemaValidation = true,
            HttpSchemaContractKey = "ReserveHttpRequest",
            HttpSchemaContractVersion = "bad-version",
            HttpSchemaRegistryProviderId = string.Empty,
            HttpSchemaStrictMode = true,
            PluginId = pluginId,
            CompensationHttpBaseUrlVariableRef = " undo.url ",
            CompensationHttpRelativePath = " /release ",
            CompensationHttpMethod = "delete",
            CompensationHttpExpectedStatusCodes = "",
            HasCompensationHttpSchemaValidation = true,
            CompensationHttpSchemaContractKey = "ReleaseHttpRequest",
            CompensationHttpSchemaContractVersion = "2.0.0",
            CompensationHttpSchemaRegistryProviderId = string.Empty,
            CompensationHttpSchemaStrictMode = true,
            CompensationPluginId = compensationPluginId
        };

        var http = Assert.IsType<HttpTaskConfiguration>(InvokePrivateStatic<ITaskConfiguration>(
            "BuildTaskConfiguration",
            TaskKind.Http,
            input,
            "knowl-default"));
        var plugin = Assert.IsType<PluginTaskConfiguration>(InvokePrivateStatic<ITaskConfiguration>(
            "BuildTaskConfiguration",
            TaskKind.Plugin,
            input,
            "knowl-default"));
        var human = Assert.IsType<HumanApprovalTaskConfiguration>(InvokePrivateStatic<ITaskConfiguration>(
            "BuildTaskConfiguration",
            TaskKind.HumanApproval,
            input,
            "knowl-default"));
        var compensationHttp = Assert.IsType<HttpTaskConfiguration>(InvokePrivateStatic<ITaskConfiguration>(
            "BuildCompensationTaskConfiguration",
            TaskKind.Http,
            input,
            "knowl-default"));
        var compensationPlugin = Assert.IsType<PluginTaskConfiguration>(InvokePrivateStatic<ITaskConfiguration>(
            "BuildCompensationTaskConfiguration",
            TaskKind.Plugin,
            input,
            "knowl-default"));
        var compensationHuman = Assert.IsType<HumanApprovalTaskConfiguration>(InvokePrivateStatic<ITaskConfiguration>(
            "BuildCompensationTaskConfiguration",
            TaskKind.HumanApproval,
            input,
            "knowl-default"));
        var fallbackCondition = InvokePrivateStatic<ExecutionCondition>(
            "BuildExecutionCondition",
            EngineType.Plugin.ToString(),
            string.Empty);
        var fallbackTransformation = InvokePrivateStatic<TransformationDefinition>(
            "BuildTransformation",
            EngineType.Plugin.ToString(),
            null!,
            null!,
            null!,
            "");
        var fallbackDispatchTypes = InvokePrivateStatic<TaskDispatchType[]>(
            "GetAllowedDispatchTypes",
            TaskKind.Http);
        var fallbackCompensationDispatchTypes = InvokePrivateStatic<TaskDispatchType[]>(
            "GetAllowedCompensationDispatchTypes",
            TaskKind.Http);
        var fallbackVersion = InvokePrivateStatic<SemanticVersion>(
            "ParseSemanticVersion",
            "1.x.0",
            new SemanticVersion(9, 9, 9));
        var fallbackStatusCodes = InvokePrivateStatic<List<int>>(
            "ParseIntList",
            "abc,0",
            new[] { 200 });

        Assert.Equal("service.url", http.BaseUrlVariableRef);
        Assert.Equal("/reserve", http.RelativePath);
        Assert.Equal("POST", http.Method);
        Assert.Equal([202, 200], http.ExpectedStatusCodes);
        Assert.Equal("ReserveHttpRequest", http.SchemaBinding!.ContractKey);
        Assert.Equal("1.0.0", http.SchemaBinding.ContractVersion.ToString());
        Assert.Equal("knowl-default", http.SchemaBinding.RegistryProviderKey);
        Assert.Equal(pluginId, plugin.PluginId.ToString());
        Assert.NotNull(human);
        Assert.Equal("undo.url", compensationHttp.BaseUrlVariableRef);
        Assert.Equal([200], compensationHttp.ExpectedStatusCodes);
        Assert.Equal("ReleaseHttpRequest", compensationHttp.SchemaBinding!.ContractKey);
        Assert.Equal(compensationPluginId, compensationPlugin.PluginId.ToString());
        Assert.NotNull(compensationHuman);
        Assert.Equal("true", ((DslConditionConfiguration)fallbackCondition.Configuration).Expression.ToString());
        Assert.Equal(string.Empty, ((DslTransformationConfiguration)fallbackTransformation.Configuration).Dsl);
        Assert.Equal([TaskDispatchType.FireAndForget, TaskDispatchType.FireAndWaitCallback], fallbackDispatchTypes);
        Assert.Equal([TaskDispatchType.FireAndForget], fallbackCompensationDispatchTypes);
        Assert.Equal(new SemanticVersion(9, 9, 9), fallbackVersion);
        Assert.Equal([200], fallbackStatusCodes);
    }

    [Fact]
    public async Task TaskConditionTransformationAndSchemaContextEndpointsUpdateAndSerializePayloads()
    {
        var fixture = new Fixture();
        SetTaskExecutionConditionCommand? conditionCommand = null;
        SetTaskTransformationCommand? transformationCommand = null;
        fixture.TaskService.SetExecutionCondition(
                Arg.Do<SetTaskExecutionConditionCommand>(command => conditionCommand = command),
                Arg.Any<CancellationToken>())
            .Returns(true);
        fixture.TaskService.SetTransformation(
                Arg.Do<SetTaskTransformationCommand>(command => transformationCommand = command),
                Arg.Any<CancellationToken>())
            .Returns(true);
        fixture.SchemaContextService.GetForTask(Arg.Any<GetTaskSchemaContextQuery>(), Arg.Any<CancellationToken>())
            .Returns(new OrchestrationSchemaContext
            {
                OrchestrationVersionId = "version-1",
                OrchestrationVersion = "1.0.0",
                StageKey = "payment",
                TaskKey = "payments.capture",
                Signature = "ctx-signature",
                Sources =
                [
                    new OrchestrationSchemaSource
                    {
                        Alias = "trigger",
                        SourceKind = OrchestrationSchemaContextSourceKind.Trigger,
                        SchemaBinding = SchemaBinding("sales.sale.created", SchemaContractKind.Event)
                    }
                ],
                Target = new OrchestrationSchemaTarget
                {
                    Alias = "request",
                    StageKey = "payment",
                    TaskKey = "payments.capture",
                    SchemaBinding = SchemaBinding("payments.capture.request", SchemaContractKind.CommandRequest)
                }
            });
        var model = fixture.CreateModel();

        var conditionResult = await model.OnPostSetTaskExecutionConditionAsync(
            new DetailsModel.SetTaskExecutionConditionRequest
            {
                TaskId = "task-1",
                HasExecutionCondition = true,
                ConditionDslExpression = "$trigger.ok"
            },
            CancellationToken.None);
        var transformationResult = await model.OnPostSetTaskTransformationAsync(
            new DetailsModel.SetTaskTransformationRequest
            {
                TaskId = "task-1",
                HasTransformation = true,
                TransformationDsl = "map request",
                SourceContextHash = "source-hash",
                TargetSchemaHash = "target-hash"
            },
            CancellationToken.None);
        var schemaContextResult = await model.OnGetTaskSchemaContextAsync("version-1", "task-1", CancellationToken.None);

        AssertJsonBoolean(conditionResult, "success", true);
        AssertJsonBoolean(transformationResult, "success", true);
        Assert.NotNull(conditionCommand);
        Assert.Equal("$trigger.ok", ((DslConditionConfiguration)conditionCommand!.ExecutionCondition!.Configuration).Expression.ToString());
        Assert.NotNull(transformationCommand);
        var transformation = Assert.IsType<DslTransformationConfiguration>(transformationCommand!.Transformation!.Configuration);
        Assert.Equal("map request", transformation.Dsl);
        Assert.Equal("source-hash", transformation.SourceContextHash);
        Assert.Equal("target-hash", transformation.TargetSchemaHash);

        var schemaJson = SerializeJsonResult(schemaContextResult);
        Assert.Equal("ctx-signature", schemaJson.RootElement.GetProperty("signature").GetString());
        Assert.Equal("trigger", schemaJson.RootElement.GetProperty("sources")[0].GetProperty("alias").GetString());
        Assert.Equal("payments.capture.request", schemaJson.RootElement.GetProperty("target").GetProperty("schema").GetProperty("contractKey").GetString());
        Assert.IsType<BadRequestResult>(await model.OnPostSetTaskExecutionConditionAsync(null!, CancellationToken.None));
        Assert.IsType<BadRequestResult>(await model.OnPostSetTaskTransformationAsync(null!, CancellationToken.None));
        Assert.IsType<BadRequestResult>(await model.OnGetTaskSchemaContextAsync(string.Empty, "task-1", CancellationToken.None));
    }

    [Fact]
    public async Task TaskSchemaContextSerializesMissingTargetAndSchemaAsEmptyPayload()
    {
        var fixture = new Fixture();
        fixture.SchemaContextService.GetForTask(Arg.Any<GetTaskSchemaContextQuery>(), Arg.Any<CancellationToken>())
            .Returns(new OrchestrationSchemaContext
            {
                OrchestrationVersionId = "version-1",
                OrchestrationVersion = "1.0.0",
                StageKey = "stage",
                TaskKey = "task",
                Signature = "empty",
                Sources =
                [
                    new OrchestrationSchemaSource
                    {
                        Alias = "previous",
                        SourceKind = OrchestrationSchemaContextSourceKind.TaskResponse,
                        StageKey = "stage-1",
                        TaskKey = "task-1",
                        SchemaBinding = null!
                    }
                ],
                Target = null!
            });
        var model = fixture.CreateModel();

        var payload = SerializeJsonResult(await model.OnGetTaskSchemaContextAsync("version-1", "task-1", CancellationToken.None));

        Assert.Equal("empty", payload.RootElement.GetProperty("signature").GetString());
        Assert.Equal("stage-1", payload.RootElement.GetProperty("sources")[0].GetProperty("stageKey").GetString());
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("sources")[0].GetProperty("schema").ValueKind);
        Assert.Equal(string.Empty, payload.RootElement.GetProperty("target").GetProperty("alias").GetString());
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("target").GetProperty("schema").ValueKind);
    }

    [Fact]
    public async Task ParallelGroupEndpointsCreateUpdateReadAndDeleteGroups()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        var groupId = Id.New().ToString();
        var createdGroup = new ParallelGroupDefinitionModel
        {
            Id = groupId,
            StageDefinitionId = stageId,
            Name = "Group3",
            JoinPolicy = ParallelJoinPolicy.WaitAll,
            MaxParallelAgents = null
        };
        fixture.ParallelGroupService.GetAll(Arg.Any<GetParallelGroupDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([
                new ParallelGroupDefinitionModel { Id = Id.New().ToString(), StageDefinitionId = stageId, Name = "Group1", JoinPolicy = ParallelJoinPolicy.WaitAll },
                new ParallelGroupDefinitionModel { Id = Id.New().ToString(), StageDefinitionId = stageId, Name = "Group2", JoinPolicy = ParallelJoinPolicy.WaitAll }
            ]);
        fixture.ParallelGroupService.Create(Arg.Any<CreateParallelGroupDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(groupId);
        fixture.ParallelGroupService.GetById(Arg.Any<GetParallelGroupDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(createdGroup);
        fixture.ParallelGroupService.Update(Arg.Any<UpdateParallelGroupDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        fixture.ParallelGroupService.Delete(Arg.Any<DeleteParallelGroupDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        fixture.TaskService.GetAll(Arg.Any<GetTaskDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([TaskModel(stageId, "task.in.group", 0, groupId)]);
        fixture.TaskService.Update(Arg.Any<UpdateTaskDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var model = fixture.CreateModel();

        var createResult = await model.OnPostCreateParallelGroupAsync(
            new DetailsModel.CreateParallelGroupRequest { StageId = stageId },
            CancellationToken.None);
        var editResult = await model.OnGetParallelGroupForEditAsync(groupId, CancellationToken.None);
        var updateResult = await model.OnPostUpdateParallelGroupAsync(
            new DetailsModel.UpdateParallelGroupRequest
            {
                GroupId = groupId,
                Name = " Updated group ",
                JoinPolicy = "WaitAll",
                MaxParallelAgents = -1
            },
            CancellationToken.None);
        var deleteResult = await model.OnPostDeleteParallelGroupAsync(
            new DetailsModel.DeleteParallelGroupRequest { StageId = stageId, GroupId = groupId },
            CancellationToken.None);

        AssertJsonBoolean(createResult, "success", true);
        AssertJsonString(editResult, "name", "Group3");
        AssertJsonBoolean(updateResult, "success", true);
        AssertJsonBoolean(deleteResult, "success", true);
        await fixture.ParallelGroupService.Received(1).Create(
            Arg.Is<CreateParallelGroupDefinitionCommand>(command =>
                command.StageDefinitionId == stageId &&
                command.Name == "Group3"),
            Arg.Any<CancellationToken>());
        await fixture.ParallelGroupService.Received(1).Update(
            Arg.Is<UpdateParallelGroupDefinitionCommand>(command =>
                command.Id == groupId &&
                command.Name == "Updated group" &&
                command.MaxParallelAgents == null),
            Arg.Any<CancellationToken>());
        await fixture.TaskService.Received(1).Update(
            Arg.Is<UpdateTaskDefinitionCommand>(command =>
                command.ExecutionMode == TaskExecutionMode.Sequential &&
                command.ParallelGroupId == string.Empty),
            Arg.Any<CancellationToken>());
        await fixture.ParallelGroupService.Received(1).Delete(
            Arg.Is<DeleteParallelGroupDefinitionCommand>(command => command.Id == groupId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostCreateTaskBuildsMessagingTaskConfigurationPoliciesAndCompensation()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        CreateTaskDefinitionCommand? captured = null;
        fixture.TaskService.GetAll(Arg.Any<GetTaskDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([TaskModel(stageId, "existing.task", 0)]);
        fixture.TaskService.Create(Arg.Do<CreateTaskDefinitionCommand>(command => captured = command), Arg.Any<CancellationToken>())
            .Returns(Id.New().ToString());
        var schemaProviderId = Id.New().ToString();
        var compensationProviderId = Id.New().ToString();
        var model = fixture.CreateModel();
        model.NewTask = new DetailsModel.CreateTaskInput
        {
            StageId = stageId,
            Key = "inventories.reserve",
            Name = "Reserve inventory",
            Kind = TaskKind.Messaging.ToString(),
            ExecutionMode = TaskExecutionMode.Sequential.ToString(),
            DispatchType = TaskDispatchType.FireAndWaitCallback.ToString(),
            OnErrorPolicy = OnErrorPolicy.StopAndCompensate.ToString(),
            Notes = "critical",
            MessagingTopic = "inventories.reserve",
            MessagingVersion = "2.1.0",
            HasMessagingSchemaValidation = true,
            MessagingSchemaContractKey = "ReserveInventory",
            MessagingSchemaContractVersion = "2.0.0",
            MessagingSchemaRegistryProviderId = schemaProviderId,
            MessagingSchemaStrictMode = true,
            HasMessagingRequestValidation = true,
            MessagingRequestValidationDsl = "$.saleId != null",
            MessagingRequestValidationErrorCode = "InvalidReserveRequest",
            HasMessagingResponseValidation = true,
            MessagingResponseValidationDsl = "$.reserved == true",
            MessagingResponseValidationErrorCode = "InvalidReserveResponse",
            HasRetryPolicy = true,
            RetryMaxRetries = 3,
            RetryDelaySeconds = 2,
            RetryableErrorCodes = "TEMP,TEMP,LOCKED",
            RetryStopOnNonRetryableError = true,
            HasTimeoutPolicy = true,
            TimeoutSeconds = 9,
            TimeoutBehavior = TimeoutBehavior.Reconcile.ToString(),
            TimeoutAction = OrchestrationActionOnTimeout.Block.ToString(),
            TimeoutReconcileRetries = 2,
            TimeoutReconcileDelaySeconds = 4,
            TimeoutReconcileRetryableErrorCodes = "TIMEOUT",
            HasCompensation = true,
            CompensationKind = TaskKind.Messaging.ToString(),
            CompensationMessagingTopic = "inventories.release",
            CompensationMessagingVersion = "1.2.3",
            HasCompensationMessagingSchemaValidation = true,
            CompensationMessagingSchemaContractKey = "ReleaseInventory",
            CompensationMessagingSchemaContractVersion = "1.0.0",
            CompensationMessagingSchemaRegistryProviderId = compensationProviderId,
            CompensationMessagingRequestValidationDsl = "$.saleId != null",
            CompensationMessagingRequestValidationErrorCode = "InvalidReleaseRequest",
            HasCompensationExecutionCondition = true,
            CompensationConditionDslExpression = "true",
            HasCompensationTransformation = true,
            CompensationTransformationDsl = "map compensation",
            HasCompensationRetryPolicy = true,
            CompensationRetryMaxRetries = 1,
            CompensationRetryableErrorCodes = "UNDO_TEMP"
        };

        var result = await model.OnPostCreateTaskAsync("orch-1", "version-1", cancellationToken: CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/OrchestrationStages/Details", redirect.PageName);
        Assert.NotNull(captured);
        Assert.Equal(1, captured!.Order);
        Assert.Equal("inventories.reserve", captured.Key);
        Assert.Equal(OnErrorPolicy.StopAndCompensate, captured.OnErrorPolicy);
        var config = Assert.IsType<MessagingTaskConfiguration>(captured.Configuration);
        Assert.Equal("inventories.reserve", config.Topic);
        Assert.Equal(new SemanticVersion(2, 1, 0), config.Version);
        Assert.True(config.HasSchemaValidation);
        Assert.Equal("ReserveInventory", config.SchemaBinding!.ContractKey);
        Assert.Equal("knowl-control", config.SchemaBinding.RegistryProviderKey);
        Assert.Equal("InvalidReserveRequest", config.RequestValidation!.ErrorCode);
        Assert.Equal("InvalidReserveResponse", config.ResponseValidation!.ErrorCode);
        Assert.Equal(3, captured.RetryPolicy!.MaxRetries);
        Assert.Equal(["TEMP", "LOCKED"], captured.RetryPolicy.RetryableErrorCodes.ToArray());
        Assert.Equal(TimeoutBehavior.Reconcile, captured.TimeoutPolicy!.TimeoutBehavior);
        Assert.Equal("inventories.release", Assert.IsType<MessagingTaskConfiguration>(captured.CompensationDefinition!.Configuration).Topic);
        Assert.True(captured.CompensationDefinition.HasExecutionCondition);
        Assert.True(captured.CompensationDefinition.HasTransformation);
        Assert.Equal(1, captured.CompensationDefinition.RetryPolicy!.MaxRetries);
    }

    [Fact]
    public async Task OnPostCreateTaskReturnsPageWithValidationErrorsWhenMessagingInputIsIncomplete()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        fixture.StageService.GetById(Arg.Any<GetStageDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(StageModel(stageId, "inventory-reservation"));
        fixture.VersionService.GetById(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(VersionModel("version-1", OrchestrationVersionStatus.Draft));
        fixture.TaskService.GetAll(Arg.Any<GetTaskDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);
        fixture.ParallelGroupService.GetAll(Arg.Any<GetParallelGroupDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var model = fixture.CreateModel();
        model.NewTask = new DetailsModel.CreateTaskInput
        {
            StageId = stageId,
            Key = "inventories.reserve",
            Name = "Reserve inventory",
            Kind = TaskKind.Messaging.ToString(),
            MessagingTopic = "",
            HasMessagingSchemaValidation = true,
            MessagingSchemaContractKey = "",
            MessagingSchemaRegistryProviderId = "not-a-ulid",
            HasMessagingRequestValidation = true,
            MessagingRequestValidationDsl = "",
            HasMessagingResponseValidation = true,
            MessagingResponseValidationDsl = "",
            HasRetryPolicy = true,
            RetryMaxRetries = -1,
            RetryDelaySeconds = -1,
            HasTimeoutPolicy = true,
            TimeoutSeconds = 0,
            TimeoutWaitSeconds = 0,
            TimeoutReconcileRetries = -1,
            TimeoutReconcileDelaySeconds = -1,
            HasCompensation = true,
            CompensationMessagingTopic = "",
            HasCompensationMessagingSchemaValidation = true,
            CompensationMessagingSchemaContractKey = "",
            CompensationMessagingSchemaRegistryProviderId = "not-a-ulid",
            HasCompensationMessagingRequestValidation = true,
            CompensationMessagingRequestValidationDsl = "",
            HasCompensationMessagingResponseValidation = true,
            CompensationMessagingResponseValidationDsl = "",
            HasCompensationRetryPolicy = true,
            CompensationRetryMaxRetries = -1,
            CompensationRetryDelaySeconds = -1,
            HasCompensationTimeoutPolicy = true,
            CompensationTimeoutSeconds = 0,
            CompensationTimeoutWaitSeconds = 0,
            CompensationTimeoutReconcileRetries = -1,
            CompensationTimeoutReconcileDelaySeconds = -1
        };

        var result = await model.OnPostCreateTaskAsync("orch-1", "version-1", cancellationToken: CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Contains("MessagingTopic", model.ModelState.Keys);
        Assert.Contains("MessagingSchemaContractKey", model.ModelState.Keys);
        Assert.Contains("MessagingRequestValidationDsl", model.ModelState.Keys);
        Assert.Contains("MessagingResponseValidationDsl", model.ModelState.Keys);
        Assert.Contains("RetryMaxRetries", model.ModelState.Keys);
        Assert.Contains("TimeoutSeconds", model.ModelState.Keys);
        Assert.Contains("CompensationMessagingTopic", model.ModelState.Keys);
        Assert.Contains("CompensationRetryMaxRetries", model.ModelState.Keys);
        await fixture.TaskService.DidNotReceive().Create(Arg.Any<CreateTaskDefinitionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostCreateTaskUpdatesExistingTaskWithEditedMessagingConfiguration()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        var task = TaskModel(stageId, "inventories.reserve", 7);
        UpdateTaskDefinitionCommand? captured = null;
        fixture.TaskService.GetById(Arg.Any<GetTaskDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(task);
        fixture.TaskService.Update(Arg.Do<UpdateTaskDefinitionCommand>(command => captured = command), Arg.Any<CancellationToken>())
            .Returns(true);
        var model = fixture.CreateModel();
        model.NewTask = new DetailsModel.CreateTaskInput
        {
            StageId = stageId,
            Key = "inventories.reserve.v2",
            Name = "Reserve inventory v2",
            Notes = "edited",
            Kind = TaskKind.Messaging.ToString(),
            ExecutionMode = TaskExecutionMode.Sequential.ToString(),
            DispatchType = TaskDispatchType.FireAndForget.ToString(),
            OnErrorPolicy = OnErrorPolicy.Stop.ToString(),
            ParallelGroupId = "invalid-group-id",
            MessagingTopic = "inventories.reserve.v2",
            MessagingVersion = "bad-version",
            HasExecutionCondition = true,
            ConditionEngine = "Unknown",
            ConditionDslExpression = "",
            HasTransformation = true,
            TransformationEngine = "Unknown",
            TransformationDsl = "map edited",
            HasRetryPolicy = true,
            RetryStrategyType = "Unknown",
            RetryMaxRetries = 0,
            RetryDelaySeconds = 0,
            RetryableErrorCodes = "TEMP,temp,LOCKED",
            HasTimeoutPolicy = true,
            TimeoutSeconds = 1,
            TimeoutBehavior = TimeoutBehavior.Fail.ToString(),
            TimeoutFailErrorCode = "",
            HasCompensation = true,
            CompensationKind = TaskKind.Messaging.ToString(),
            CompensationDispatchType = TaskDispatchType.FireAndWaitCallback.ToString(),
            CompensationMessagingTopic = "inventories.release",
            CompensationMessagingVersion = "0.bad",
            HasCompensationTimeoutPolicy = true,
            CompensationTimeoutBehavior = TimeoutBehavior.Wait.ToString(),
            CompensationTimeoutSeconds = 1,
            CompensationTimeoutWaitSeconds = 1
        };

        var result = await model.OnPostCreateTaskAsync("orch-1", "version-1", task.Id, CancellationToken.None);

        Assert.Equal("/OrchestrationStages/Details", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.NotNull(captured);
        Assert.Equal(task.Id, captured!.Id);
        Assert.Equal("inventories.reserve.v2", captured.Key);
        Assert.Equal(7, captured.Order);
        Assert.Equal(string.Empty, captured.ParallelGroupId);
        Assert.Equal(TaskDispatchType.FireAndForget, captured.DispatchType);
        Assert.NotNull(captured.ExecutionCondition);
        Assert.Equal("true", ((DslConditionConfiguration)captured.ExecutionCondition!.Configuration).Expression.ToString());
        Assert.Equal("map edited", ((DslTransformationConfiguration)captured.Transformation!.Configuration).Dsl);
        Assert.Equal(0, captured.RetryPolicy!.MaxRetries);
        Assert.Equal(0, ((FixedRetryStrategy)captured.RetryPolicy.Strategy).Delay.Value.TotalSeconds);
        Assert.Equal(["TEMP", "LOCKED"], captured.RetryPolicy.RetryableErrorCodes.ToArray());
        Assert.Equal(1, captured.TimeoutPolicy!.Timeout.Value.TotalSeconds);
        Assert.Equal("TIMEOUT", ((FailTimeoutBehaviorPolicy)captured.TimeoutPolicy.TimeoutBehaviorPolicy).ErrorCode);
        var config = Assert.IsType<MessagingTaskConfiguration>(captured.Configuration);
        Assert.Equal("inventories.reserve.v2", config.Topic);
        Assert.Equal(new SemanticVersion(1, 0, 0), config.Version);
        Assert.NotNull(captured.CompensationDefinition);
        Assert.Equal(TaskDispatchType.FireAndForget, captured.CompensationDefinition!.DispatchType);
        Assert.Equal(1, captured.CompensationDefinition.TimeoutPolicy!.Timeout.Value.TotalSeconds);
        Assert.Equal(1, ((WaitTimeoutBehaviorPolicy)captured.CompensationDefinition.TimeoutPolicy.TimeoutBehaviorPolicy).WaitingTime.Value.TotalSeconds);
    }

    [Fact]
    public async Task DetailEndpointsReturnNotFoundWhenRequestedEntityDoesNotExist()
    {
        var fixture = new Fixture();
        fixture.TaskService.GetById(Arg.Any<GetTaskDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns((TaskDefinitionModel)null!);
        fixture.ParallelGroupService.GetById(Arg.Any<GetParallelGroupDefinitionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns((ParallelGroupDefinitionModel)null!);
        fixture.ParallelGroupService.GetAll(Arg.Any<GetParallelGroupDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);
        fixture.ParallelGroupService.Create(Arg.Any<CreateParallelGroupDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Id.New().ToString());
        var model = fixture.CreateModel();

        Assert.IsType<NotFoundResult>(await model.OnGetTaskForEditAsync("missing-task", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await model.OnGetTaskExecutionConditionForEditAsync("missing-task", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await model.OnGetTaskTransformationForEditAsync("missing-task", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await model.OnGetParallelGroupForEditAsync("missing-group", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await model.OnPostUpdateParallelGroupAsync(
            new DetailsModel.UpdateParallelGroupRequest { GroupId = "missing-group" },
            CancellationToken.None));
        Assert.IsType<BadRequestResult>(await model.OnPostCreateParallelGroupAsync(
            new DetailsModel.CreateParallelGroupRequest { StageId = "stage-1" },
            CancellationToken.None));
        Assert.IsType<BadRequestResult>(await model.OnPostDeleteParallelGroupAsync(
            new DetailsModel.DeleteParallelGroupRequest { StageId = "stage-1", GroupId = "not-a-ulid" },
            CancellationToken.None));
        Assert.IsType<BadRequestResult>(await model.OnPostCreateParallelGroupAsync(null!, CancellationToken.None));
        Assert.IsType<BadRequestResult>(await model.OnGetParallelGroupForEditAsync(string.Empty, CancellationToken.None));
        Assert.IsType<BadRequestResult>(await model.OnPostUpdateParallelGroupAsync(
            new DetailsModel.UpdateParallelGroupRequest(),
            CancellationToken.None));
        Assert.IsType<BadRequestResult>(await model.OnPostDeleteParallelGroupAsync(
            new DetailsModel.DeleteParallelGroupRequest { StageId = "", GroupId = "" },
            CancellationToken.None));
    }

    [Fact]
    public async Task OnPostCreateTaskUsesMessagingDefaultsWhenOptionalPoliciesAreDisabled()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        CreateTaskDefinitionCommand? captured = null;
        fixture.TaskService.GetAll(Arg.Any<GetTaskDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);
        fixture.TaskService.Create(Arg.Do<CreateTaskDefinitionCommand>(command => captured = command), Arg.Any<CancellationToken>())
            .Returns(Id.New().ToString());
        var model = fixture.CreateModel();
        model.NewTask = new DetailsModel.CreateTaskInput
        {
            StageId = stageId,
            Key = "payments.capture",
            Name = "Capture payment",
            Kind = TaskKind.Messaging.ToString(),
            ExecutionMode = TaskExecutionMode.Sequential.ToString(),
            DispatchType = TaskDispatchType.FireAndWaitCallback.ToString(),
            OnErrorPolicy = OnErrorPolicy.Stop.ToString(),
            MessagingTopic = "payments.capture",
            MessagingVersion = "",
            HasCompensation = false,
            HasRetryPolicy = false,
            HasTimeoutPolicy = false
        };

        var result = await model.OnPostCreateTaskAsync("orch-1", "version-1", cancellationToken: CancellationToken.None);

        Assert.Equal("/OrchestrationStages/Details", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.NotNull(captured);
        Assert.Equal(0, captured!.Order);
        Assert.Null(captured.ExecutionCondition);
        Assert.Null(captured.Transformation);
        Assert.Null(captured.RetryPolicy);
        Assert.Null(captured.TimeoutPolicy);
        Assert.Null(captured.CompensationDefinition);
        var config = Assert.IsType<MessagingTaskConfiguration>(captured.Configuration);
        Assert.Equal("payments.capture", config.Topic);
        Assert.Equal(new SemanticVersion(1, 0, 0), config.Version);
        Assert.False(config.HasSchemaValidation);
        Assert.False(config.HasRequestValidation);
        Assert.False(config.HasResponseValidation);
    }

    [Fact]
    public async Task DeleteEndpointsRemoveTaskNormalizeOrderAndDeleteStage()
    {
        var fixture = new Fixture();
        var stageId = Id.New().ToString();
        var task = TaskModel(stageId, "task.remaining", 5);
        fixture.TaskService.Delete(Arg.Any<DeleteTaskDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        fixture.TaskService.GetAll(Arg.Any<GetTaskDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns([task]);
        fixture.TaskService.Update(Arg.Any<UpdateTaskDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        fixture.StageService.Delete(Arg.Any<DeleteStageDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var model = fixture.CreateModel();

        var deleteTaskResult = await model.OnPostDeleteTaskAsync("orch-1", "version-1", stageId, "task-deleted", CancellationToken.None);
        var deleteStageResult = await model.OnPostDeleteStageAsync("orch-1", "version-1", stageId, CancellationToken.None);

        Assert.Equal("/OrchestrationStages/Details", Assert.IsType<RedirectToPageResult>(deleteTaskResult).PageName);
        Assert.Equal("/OrchestrationVersions/Details", Assert.IsType<RedirectToPageResult>(deleteStageResult).PageName);
        await fixture.TaskService.Received(1).Delete(
            Arg.Is<DeleteTaskDefinitionCommand>(command => command.Id == "task-deleted"),
            Arg.Any<CancellationToken>());
        await fixture.TaskService.Received(1).Update(
            Arg.Is<UpdateTaskDefinitionCommand>(command => command.Id == task.Id && command.Order == 0),
            Arg.Any<CancellationToken>());
        await fixture.StageService.Received(1).Delete(
            Arg.Is<DeleteStageDefinitionCommand>(command => command.Id == stageId),
            Arg.Any<CancellationToken>());
    }

    private static TaskDefinitionModel TaskModel(
        string stageId,
        string key,
        int order,
        string parallelGroupId = "")
        => new()
        {
            Id = Id.New().ToString(),
            StageDefinitionId = stageId,
            Key = key,
            Name = key,
            Order = order,
            Notes = string.Empty,
            Kind = TaskKind.Messaging,
            ExecutionMode = string.IsNullOrWhiteSpace(parallelGroupId)
                ? TaskExecutionMode.Sequential
                : TaskExecutionMode.Parallel,
            ParallelGroupId = parallelGroupId,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = key,
                Version = new SemanticVersion(1, 0, 0)
            },
            DispatchType = TaskDispatchType.FireAndWaitCallback,
            OnErrorPolicy = OnErrorPolicy.Stop,
            IsEnabled = true
        };

    private static StageDefinitionModel StageModel(string stageId, string key)
        => new()
        {
            Id = stageId,
            OrchestrationVersionId = "version-1",
            Key = key,
            Name = key,
            Order = 0
        };

    private static OrchestrationVersionModel VersionModel(string versionId, OrchestrationVersionStatus status)
        => new()
        {
            Id = versionId,
            OrchestrationDefinitionId = "orch-1",
            Version = "1.0.0",
            Status = status
        };

    private static ExecutionCondition DslCondition(string expression)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression(expression) }
        };

    private static TransformationDefinition DslTransformation(
        string dsl,
        string sourceContextHash = "",
        string targetSchemaHash = "")
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration
            {
                Dsl = dsl,
                SourceContextHash = sourceContextHash,
                TargetSchemaHash = targetSchemaHash
            }
        };

    private static SchemaBinding SchemaBinding(string contractKey, SchemaContractKind contractKind)
        => new()
        {
            Id = Id.New(),
            ElementType = ElementType.Task,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = contractKey,
            ContractVersion = new SemanticVersion(1, 0, 0),
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "knowl-control",
            ContractKind = contractKind,
            StrictMode = true,
            IsValidationEnabled = true,
            Snapshot = new Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot
            {
                ContractKind = contractKind,
                RegistryProviderKey = "knowl-control",
                ContractKey = contractKey,
                ContractVersion = "1.0.0",
                SchemaFormat = "ButterMorph",
                SchemaJson = """{"type":"object"}""",
                ContentHash = $"hash-{contractKey}",
                SourceArtifactId = $"artifact-{contractKey}",
                ResolvedBy = "tests",
                ResolvedAtUtc = DateTimeOffset.UtcNow
            }
        };

    private static RetryPolicy FixedRetryPolicy(
        int maxRetries,
        double delaySeconds,
        IReadOnlyCollection<string> retryableErrorCodes,
        bool stopOnNonRetryableError)
        => new()
        {
            MaxRetries = maxRetries,
            StrategyType = RetryStrategyType.Fixed,
            Strategy = new FixedRetryStrategy { Delay = Duration.FromSeconds(delaySeconds) },
            RetryableErrorCodes = retryableErrorCodes.ToList(),
            StopOnNonRetryableError = stopOnNonRetryableError
        };

    private static void AssertJsonBoolean(IActionResult result, string propertyName, bool expected)
    {
        var json = Assert.IsType<JsonResult>(result);
        var document = JsonSerializer.SerializeToDocument(json.Value);
        Assert.Equal(expected, document.RootElement.GetProperty(propertyName).GetBoolean());
    }

    private static void InvokePrivate(DetailsModel model, string methodName, params object?[] args)
    {
        var method = typeof(DetailsModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(model, args);
    }

    private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
    {
        var method = typeof(DetailsModel).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        return (T)method.Invoke(null, args)!;
    }

    private static void AssertJsonString(IActionResult result, string propertyName, string expected)
    {
        var document = SerializeJsonResult(result);
        Assert.Equal(expected, document.RootElement.GetProperty(propertyName).GetString());
    }

    private static JsonDocument SerializeJsonResult(IActionResult result)
    {
        var json = Assert.IsType<JsonResult>(result);
        return JsonSerializer.SerializeToDocument(json.Value);
    }

    private sealed class Fixture
    {
        public IStageApplicationService StageService { get; } = Substitute.For<IStageApplicationService>();

        public ITaskApplicationService TaskService { get; } = Substitute.For<ITaskApplicationService>();

        public IParallelGroupApplicationService ParallelGroupService { get; } = Substitute.For<IParallelGroupApplicationService>();

        public IOrchestrationVersionApplicationService VersionService { get; } = Substitute.For<IOrchestrationVersionApplicationService>();

        public IOrchestrationSchemaContextApplicationService SchemaContextService { get; } = Substitute.For<IOrchestrationSchemaContextApplicationService>();

        public DetailsModel CreateModel()
            => new(
                StageService,
                TaskService,
                ParallelGroupService,
                VersionService,
                SchemaContextService,
                Options.Create(new OrchestratorDesignWebUIOptions
                {
                    DefaultSchemaRegistryProviderKey = "knowl-control"
                }));
    }
}
