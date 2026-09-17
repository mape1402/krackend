using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DesignSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class ControlPlaneEntityFrameworkDesignRepositoryTests
{
    [Fact]
    public async Task DesignRepositoriesPersistAndReadCompleteMessagingDefinition()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        var domainRepository = services.GetRequiredService<IDomainRepository>();
        var definitionRepository = services.GetRequiredService<IOrchestrationDefinitionRepository>();
        var versionRepository = services.GetRequiredService<IOrchestrationVersionRepository>();
        var stageRepository = services.GetRequiredService<IStageRepository>();
        var taskRepository = services.GetRequiredService<ITaskRepository>();
        var triggerRepository = services.GetRequiredService<ITriggerBindingRepository>();
        var variableRepository = services.GetRequiredService<IVariableDefinitionRepository>();
        var parallelRepository = services.GetRequiredService<IParallelGroupRepository>();
        var branchRepository = services.GetRequiredService<IBranchRuleRepository>();

        var domain = new Domain
        {
            Id = Id.New(),
            Key = "sales",
            DisplayName = "Sales",
            Description = "Sales domain",
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
        };

        await domainRepository.Upsert(domain);

        var definition = new OrchestrationDefinition
        {
            Id = Id.New(),
            Key = "sales.sale.created",
            Name = "Sale Created",
            Description = "Happy path",
            Domain = domain.Key,
            DomainId = domain.Id,
            IsActive = true,
            Tags = ["sales", "messaging"],
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests",
        };

        await definitionRepository.Create(definition);

        var version = new OrchestrationVersion
        {
            Id = Id.New(),
            OrchestrationDefinitionId = definition.Id,
            Version = new SemanticVersion(1, 2, 3),
            Status = OrchestrationVersionStatus.Draft,
            VersionLabel = "1.2.3",
            Description = "Version under test",
            Checksum = new Checksum("sha256:definition"),
            Notes = "Roundtrip",
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests",
        };

        await versionRepository.Create(version);

        var stage = new StageDefinition
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "fulfillment",
            Name = "Fulfillment",
            Description = "Reserves inventory and charges payment",
            Order = 2,
            HasExecutionCondition = true,
            ExecutionCondition = DslCondition("$trigger.ShouldFulfill"),
        };

        await stageRepository.Create(stage);

        var parallelGroup = new ParallelGroupDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Name = "External operations",
            JoinPolicy = ParallelJoinPolicy.WaitAll,
            MaxParallelAgents = null,
        };

        await parallelRepository.Create(parallelGroup);

        var messagingTask = new TaskDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Key = "inventories.reserve",
            Name = "Reserve inventory",
            Order = 1,
            Notes = "Uses request and response schemas",
            Kind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Parallel,
            ParallelGroupId = parallelGroup.Id,
            HasExecutionCondition = true,
            ExecutionCondition = DslCondition("$trigger.CanReserve"),
            HasTransformation = true,
            Transformation = DslTransformation("map reserve request"),
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "inventories.reserve",
                Version = new SemanticVersion(1, 0, 0),
                HasSchemaValidation = true,
                RequestSchemaBinding = SchemaBinding(messagingTaskId: null, ElementType.Task, SchemaContractKind.CommandRequest, "inventories.reserve.request"),
                ResponseSchemaBinding = SchemaBinding(messagingTaskId: null, ElementType.Task, SchemaContractKind.CommandResponse, "inventories.reserve.response"),
                HasRequestValidation = true,
                RequestValidation = DslValidation("validate reserve request", "ReserveRequestInvalid"),
                HasResponseValidation = true,
                ResponseValidation = DslValidation("validate reserve response", "ReserveResponseInvalid"),
            },
            RetryPolicy = RetryPolicy(),
            TimeoutPolicy = TimeoutFailPolicy(),
            OnErrorPolicy = OnErrorPolicy.StopAndCompensate,
            CompensationDefinition = new CompensationDefinition
            {
                CompensationTaskKind = TaskKind.Messaging,
                DispatchType = TaskDispatchType.FireAndWaitCallback,
                HasTransformation = true,
                Transformation = DslTransformation("map release inventory"),
                HasExecutionCondition = true,
                ExecutionCondition = DslCondition("$tasks['inventories.reserve'].Succeeded"),
                Configuration = new MessagingTaskConfiguration
                {
                    Topic = "inventories.release",
                    Version = new SemanticVersion(1, 0, 0),
                    RequestSchemaBinding = SchemaBinding(null, ElementType.Task, SchemaContractKind.CommandRequest, "inventories.release.request"),
                    HasRequestValidation = true,
                    RequestValidation = DslValidation("validate release request", "ReleaseRequestInvalid"),
                },
                RetryPolicy = RetryPolicy(),
                TimeoutPolicy = TimeoutWaitPolicy(),
            },
            DispatchType = TaskDispatchType.FireAndWaitCallback,
            IsEnabled = true,
        };

        var requestBinding = ((MessagingTaskConfiguration)messagingTask.Configuration).RequestSchemaBinding;
        var responseBinding = ((MessagingTaskConfiguration)messagingTask.Configuration).ResponseSchemaBinding;
        requestBinding.ElementId = messagingTask.Id;
        responseBinding.ElementId = messagingTask.Id;

        await taskRepository.Create(messagingTask);

        var httpTask = new TaskDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Key = "notifications.email",
            Name = "Send email",
            Order = 2,
            Kind = TaskKind.Http,
            ExecutionMode = TaskExecutionMode.Sequential,
            HasTransformation = false,
            Configuration = new HttpTaskConfiguration
            {
                BaseUrlVariableRef = "notifications.base-url",
                RelativePath = "/api/email",
                Method = "POST",
                HeadersTemplate = JsonNode.Parse("""{"x-flow":"sale"}"""),
                QueryTemplate = JsonNode.Parse("""{"priority":"normal"}"""),
                ExpectedStatusCodes = [200, 202],
                AllowSyncResponse = true,
                HasSchemaValidation = true,
                SchemaBinding = SchemaBinding(null, ElementType.Task, SchemaContractKind.CommandRequest, "notifications.email.request"),
            },
            DispatchType = TaskDispatchType.FireAndWait,
            IsEnabled = true,
        };

        ((HttpTaskConfiguration)httpTask.Configuration).SchemaBinding.ElementId = httpTask.Id;

        await taskRepository.Create(httpTask);

        var pluginTask = new TaskDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Key = "audit.plugin",
            Name = "Audit plugin",
            Order = 3,
            Kind = TaskKind.Plugin,
            ExecutionMode = TaskExecutionMode.Sequential,
            Configuration = new PluginTaskConfiguration { PluginId = Id.New() },
            DispatchType = TaskDispatchType.FireAndForget,
            IsEnabled = true,
        };

        await taskRepository.Create(pluginTask);

        var humanTask = new TaskDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Key = "approval.manual",
            Name = "Manual approval",
            Order = 4,
            Kind = TaskKind.HumanApproval,
            ExecutionMode = TaskExecutionMode.Sequential,
            Configuration = new HumanApprovalTaskConfiguration(),
            DispatchType = TaskDispatchType.FireAndForget,
            IsEnabled = false,
        };

        await taskRepository.Create(humanTask);

        var branchRule = new BranchRuleDefinition
        {
            Id = Id.New(),
            FromType = ElementType.Stage,
            FromId = stage.Id,
            Condition = DslCondition("$tasks['inventories.reserve'].Succeeded"),
            NavigateToType = ElementType.Task,
            NavigateToId = httpTask.Id,
        };

        await branchRepository.Create(branchRule);

        var trigger = new TriggerBinding
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "sale-created",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            Description = "Sale created event",
            TriggerChannel = new EventTriggerChannel
            {
                Topic = "events.sales.sale.created",
                Version = new SemanticVersion(1, 2, 3),
                HasSchemaValidation = true,
                SchemaBinding = SchemaBinding(null, ElementType.Orchestration, SchemaContractKind.Event, "sales.sale.created"),
                HasValidation = true,
                Validation = DslValidation("validate trigger", "SaleCreatedInvalid"),
            },
        };

        trigger.TriggerChannel.SchemaBinding.ElementId = version.Id;
        await triggerRepository.Create(trigger);

        var variable = new VariableDefinition
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "notifications.base-url",
            DisplayName = "Notifications base url",
            Description = "HTTP endpoint",
            Scope = VariableScope.Definition,
            ValueType = VariableValueType.String,
            DefaultValue = "https://notifications.local",
            IsRequired = true,
            IsSensitive = false,
        };

        await variableRepository.Create(variable);

        var persistedDefinition = await definitionRepository.GetById(definition.Id);
        var persistedVersion = await versionRepository.GetById(version.Id);
        var persistedStage = await stageRepository.GetById(stage.Id);
        var persistedTasks = (await taskRepository.GetAll(stage.Id)).ToArray();
        var persistedTrigger = await triggerRepository.GetById(trigger.Id);
        var persistedVariables = (await variableRepository.GetAll(version.Id)).ToArray();
        var persistedBranches = (await branchRepository.GetAll(stage.Id)).ToArray();

        Assert.Equal("Sales", persistedDefinition.DomainDisplayName);
        Assert.Equal(new SemanticVersion(1, 2, 3).ToString(), persistedVersion.Version.ToString());
        Assert.True(persistedStage.HasExecutionCondition);
        Assert.Equal(4, persistedTasks.Length);
        Assert.Single(persistedStage.ParallelGroups);
        Assert.Single(persistedStage.BranchRules);
        Assert.Single(persistedBranches);
        Assert.Single(persistedVariables);

        var persistedMessaging = Assert.IsType<MessagingTaskConfiguration>(persistedTasks[0].Configuration);
        Assert.Equal("inventories.reserve", persistedMessaging.Topic);
        Assert.True(persistedMessaging.HasRequestValidation);
        Assert.True(persistedMessaging.HasResponseValidation);
        Assert.Equal(SchemaContractKind.CommandRequest, persistedMessaging.RequestSchemaBinding.ContractKind);
        Assert.Equal(SchemaContractKind.CommandResponse, persistedMessaging.ResponseSchemaBinding.ContractKind);
        Assert.Equal("ReserveResponseInvalid", persistedMessaging.ResponseValidation.ErrorCode);
        Assert.Equal(2, persistedTasks[0].RetryPolicy.MaxRetries);
        Assert.IsType<FailTimeoutBehaviorPolicy>(persistedTasks[0].TimeoutPolicy.TimeoutBehaviorPolicy);
        Assert.IsType<MessagingTaskConfiguration>(persistedTasks[0].CompensationDefinition.Configuration);

        var persistedHttp = Assert.IsType<HttpTaskConfiguration>(persistedTasks[1].Configuration);
        Assert.True(persistedHttp.AllowSyncResponse);
        Assert.Equal([200, 202], persistedHttp.ExpectedStatusCodes);
        Assert.Equal(SchemaContractKind.CommandRequest, persistedHttp.SchemaBinding.ContractKind);

        Assert.IsType<PluginTaskConfiguration>(persistedTasks[2].Configuration);
        Assert.IsType<HumanApprovalTaskConfiguration>(persistedTasks[3].Configuration);

        var eventChannel = Assert.IsType<EventTriggerChannel>(persistedTrigger.TriggerChannel);
        Assert.Equal("events.sales.sale.created", eventChannel.Topic);
        Assert.True(eventChannel.HasValidation);
        Assert.Equal(SchemaContractKind.Event, eventChannel.SchemaBinding.ContractKind);
    }

    [Fact]
    public async Task DesignRepositoriesUpdateDeleteAndPageDefinitions()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        var domainRepository = services.GetRequiredService<IDomainRepository>();
        var definitionRepository = services.GetRequiredService<IOrchestrationDefinitionRepository>();
        var versionRepository = services.GetRequiredService<IOrchestrationVersionRepository>();
        var stageRepository = services.GetRequiredService<IStageRepository>();
        var taskRepository = services.GetRequiredService<ITaskRepository>();
        var triggerRepository = services.GetRequiredService<ITriggerBindingRepository>();
        var variableRepository = services.GetRequiredService<IVariableDefinitionRepository>();
        var parallelRepository = services.GetRequiredService<IParallelGroupRepository>();
        var branchRepository = services.GetRequiredService<IBranchRuleRepository>();

        var domain = new Domain
        {
            Id = Id.New(),
            Key = "billing",
            DisplayName = "Billing",
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
        };

        await domainRepository.Upsert(domain);
        await domainRepository.SetIsActive(domain.Id, false);

        var inactiveDomain = await domainRepository.GetByKey("billing");
        Assert.False(inactiveDomain.IsActive);

        var definition = new OrchestrationDefinition
        {
            Id = Id.New(),
            Key = "billing.invoice.created",
            Name = "Invoice Created",
            Domain = domain.Key,
            DomainId = domain.Id,
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests",
            IsActive = true,
        };

        await definitionRepository.Create(definition);
        definition.Name = "Invoice Created Updated";
        definition.Tags = ["billing", "updated"];
        await definitionRepository.Update(definition);
        await definitionRepository.SetIsActive(definition.Id, false);

        var versionOne = Version(definition.Id, 1);
        var versionTwo = Version(definition.Id, 2);
        await versionRepository.Create(versionOne);
        await versionRepository.Create(versionTwo);
        await versionRepository.SetStatus(versionTwo.Id, OrchestrationVersionStatus.Approved);

        var stage = new StageDefinition
        {
            Id = Id.New(),
            OrchestrationVersionId = versionTwo.Id,
            Key = "capture",
            Name = "Capture",
            Order = 1,
        };

        await stageRepository.Create(stage);
        await stageRepository.SetExecutionCondition(stage.Id, DslCondition("$trigger.ShouldCapture"));

        var parallelGroup = new ParallelGroupDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Name = "Capture group",
            JoinPolicy = ParallelJoinPolicy.WaitAll,
            MaxParallelAgents = 4,
        };
        await parallelRepository.Create(parallelGroup);
        parallelGroup.MaxParallelAgents = 8;
        await parallelRepository.Update(parallelGroup);

        var task = new TaskDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Key = "payments.capture",
            Name = "Capture payment",
            Order = 1,
            Kind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "payments.capture",
                Version = new SemanticVersion(1, 0, 0),
            },
            DispatchType = TaskDispatchType.FireAndWaitCallback,
            IsEnabled = true,
        };

        await taskRepository.Create(task);
        await taskRepository.SetIsEnabled(task.Id, false);
        await taskRepository.SetExecutionCondition(task.Id, DslCondition("$trigger.Total > 0"));
        await taskRepository.SetTransformation(task.Id, DslTransformation("map capture"));

        var branch = new BranchRuleDefinition
        {
            Id = Id.New(),
            FromType = ElementType.Stage,
            FromId = stage.Id,
            Condition = DslCondition("$tasks['payments.capture'].Succeeded"),
            NavigateToType = ElementType.Task,
            NavigateToId = task.Id,
        };
        await branchRepository.Create(branch);
        branch.Condition = DslCondition("$tasks['payments.capture'].Status == 'Succeeded'");
        await branchRepository.Update(branch);

        var trigger = new TriggerBinding
        {
            Id = Id.New(),
            OrchestrationVersionId = versionTwo.Id,
            Key = "invoice-created",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new EventTriggerChannel
            {
                Topic = "events.billing.invoice.created",
                Version = new SemanticVersion(2, 0, 0),
            },
        };
        await triggerRepository.Create(trigger);
        await triggerRepository.SetIsEnabled(trigger.Id, false);

        var variable = new VariableDefinition
        {
            Id = Id.New(),
            OrchestrationVersionId = versionTwo.Id,
            Key = "payment.timeout",
            Scope = VariableScope.Definition,
            ValueType = VariableValueType.TimeSpan,
            DefaultValue = "00:00:30",
            IsRequired = true,
        };
        await variableRepository.Create(variable);
        variable.DefaultValue = "00:01:00";
        await variableRepository.Update(variable);

        var paged = await definitionRepository.GetAll(new PagedSettings(1, 10, [], []));
        var versions = await versionRepository.GetAll(definition.Id, new PagedSettings(1, 10, [], []));
        var latest = await versionRepository.GetLatestByOrchestrationDefinitionId(definition.Id, versionTwo.Id);
        var persistedTask = await taskRepository.GetById(task.Id);
        var persistedBranch = await branchRepository.GetById(branch.Id);
        var persistedParallelGroup = await parallelRepository.GetById(parallelGroup.Id);
        var persistedTrigger = await triggerRepository.GetById(trigger.Id);
        var persistedVariable = await variableRepository.GetById(variable.Id);

        Assert.Single(paged.Rows);
        Assert.False(paged.Rows.Single().IsActive);
        Assert.Equal(2, versions.Rows.Count());
        Assert.Equal(versionOne.Id, latest.Id);
        Assert.False(persistedTask.IsEnabled);
        Assert.True(persistedTask.HasExecutionCondition);
        Assert.True(persistedTask.HasTransformation);
        Assert.NotNull(persistedBranch.Condition);
        Assert.Equal(8, persistedParallelGroup.MaxParallelAgents);
        Assert.False(persistedTrigger.IsEnabled);
        Assert.Equal("00:01:00", persistedVariable.DefaultValue);

        await branchRepository.Delete(branch.Id);
        await parallelRepository.Delete(parallelGroup.Id);
        await triggerRepository.Delete(trigger.Id);
        await variableRepository.Delete(variable.Id);
        await taskRepository.Delete(task.Id);
        await stageRepository.Delete(stage.Id);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => stageRepository.GetById(stage.Id));
        Assert.Empty(await taskRepository.GetAll(stage.Id));
        Assert.Empty(await triggerRepository.GetAll(versionTwo.Id));
        Assert.Empty(await variableRepository.GetAll(versionTwo.Id));
    }

    [Fact]
    public async Task DesignRepositoriesPreserveDisabledOptionalConfigurationAndValidationDefaults()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var definitionRepository = services.GetRequiredService<IOrchestrationDefinitionRepository>();
        var versionRepository = services.GetRequiredService<IOrchestrationVersionRepository>();
        var stageRepository = services.GetRequiredService<IStageRepository>();
        var taskRepository = services.GetRequiredService<ITaskRepository>();
        var triggerRepository = services.GetRequiredService<ITriggerBindingRepository>();
        var definition = new OrchestrationDefinition
        {
            Id = Id.New(),
            Key = "sales.sale.cancelled",
            Name = "Sale Cancelled",
            Domain = "sales",
            DomainId = Id.New(),
            OwnerTeam = "fulfillment",
            OwnerTeamId = Id.New(),
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests",
            IsActive = true,
            Tags = null,
        };
        await definitionRepository.Create(definition);
        var version = Version(definition.Id, 9);
        await versionRepository.Create(version);
        var stage = new StageDefinition
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "cancel",
            Name = "Cancel",
            Order = 1,
            HasExecutionCondition = false,
            ExecutionCondition = DslCondition("$trigger.ShouldCancel"),
        };
        await stageRepository.Create(stage);
        var requestBinding = SchemaBinding(null, ElementType.Task, SchemaContractKind.CommandRequest, "sales.cancel.request");
        var responseBinding = SchemaBinding(null, ElementType.Task, SchemaContractKind.CommandResponse, "sales.cancel.response");
        var task = new TaskDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Key = "sales.cancel",
            Name = "Cancel sale",
            Order = 1,
            Kind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            HasExecutionCondition = false,
            ExecutionCondition = DslCondition("$trigger.Total > 0"),
            HasTransformation = false,
            Transformation = DslTransformation("should stay disabled"),
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "sales.cancel",
                Version = new SemanticVersion(9, 0, 0),
                HasSchemaValidation = false,
                SchemaBinding = requestBinding,
                RequestSchemaBinding = requestBinding,
                ResponseSchemaBinding = responseBinding,
                HasRequestValidation = true,
                RequestValidation = null,
                HasResponseValidation = true,
                ResponseValidation = null,
            },
            RetryPolicy = null,
            TimeoutPolicy = TimeoutReconcilePolicy(),
            CompensationDefinition = new CompensationDefinition
            {
                CompensationTaskKind = TaskKind.Http,
                DispatchType = TaskDispatchType.FireAndWait,
                HasTransformation = false,
                HasExecutionCondition = false,
                Configuration = new HttpTaskConfiguration
                {
                    BaseUrlVariableRef = "sales.base-url",
                    RelativePath = "/api/cancel/undo",
                    Method = "POST",
                    ExpectedStatusCodes = [],
                    AllowSyncResponse = false,
                    HasSchemaValidation = false,
                    SchemaBinding = SchemaBinding(null, ElementType.Task, SchemaContractKind.CommandRequest, "sales.cancel.undo.request"),
                },
                TimeoutPolicy = TimeoutReconcilePolicy(),
            },
            DispatchType = TaskDispatchType.FireAndWaitCallback,
            IsEnabled = true,
        };
        requestBinding.ElementId = task.Id;
        responseBinding.ElementId = task.Id;
        await taskRepository.Create(task);
        var trigger = new TriggerBinding
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "sale-cancelled",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new EventTriggerChannel
            {
                Topic = "events.sales.sale.cancelled",
                Version = new SemanticVersion(9, 0, 0),
                HasSchemaValidation = false,
                SchemaBinding = SchemaBinding(null, ElementType.Orchestration, SchemaContractKind.Event, "sales.sale.cancelled"),
                HasValidation = false,
                Validation = DslValidation("disabled trigger validation", string.Empty),
            },
        };
        trigger.TriggerChannel.SchemaBinding.ElementId = version.Id;
        await triggerRepository.Create(trigger);

        var persistedDefinition = await definitionRepository.GetById(definition.Id);
        var persistedStage = await stageRepository.GetById(stage.Id);
        var persistedTask = await taskRepository.GetById(task.Id);
        var persistedTrigger = await triggerRepository.GetById(trigger.Id);

        Assert.Empty(persistedDefinition.Tags);
        Assert.Equal("fulfillment", persistedDefinition.OwnerTeamDisplayName);
        Assert.False(persistedStage.HasExecutionCondition);
        Assert.Null(persistedStage.ExecutionCondition);
        Assert.False(persistedTask.HasExecutionCondition);
        Assert.Null(persistedTask.ExecutionCondition);
        Assert.False(persistedTask.HasTransformation);
        Assert.Null(persistedTask.Transformation);
        Assert.Null(persistedTask.RetryPolicy);
        Assert.IsType<ReconcileTimeoutBehaviorPolicy>(persistedTask.TimeoutPolicy.TimeoutBehaviorPolicy);
        var persistedMessaging = Assert.IsType<MessagingTaskConfiguration>(persistedTask.Configuration);
        Assert.True(persistedMessaging.HasSchemaValidation);
        Assert.True(persistedMessaging.HasRequestValidation);
        Assert.True(persistedMessaging.HasResponseValidation);
        Assert.Equal("RequestValidationFailed", persistedMessaging.RequestValidation.ErrorCode);
        Assert.Equal("ResponseValidationFailed", persistedMessaging.ResponseValidation.ErrorCode);
        Assert.Equal(SchemaContractKind.CommandRequest, persistedMessaging.RequestSchemaBinding.ContractKind);
        Assert.Equal(SchemaContractKind.CommandResponse, persistedMessaging.ResponseSchemaBinding.ContractKind);
        var compensationHttp = Assert.IsType<HttpTaskConfiguration>(persistedTask.CompensationDefinition.Configuration);
        Assert.False(compensationHttp.HasSchemaValidation);
        Assert.Empty(compensationHttp.ExpectedStatusCodes);
        Assert.IsType<ReconcileTimeoutBehaviorPolicy>(persistedTask.CompensationDefinition.TimeoutPolicy.TimeoutBehaviorPolicy);
        var persistedEvent = Assert.IsType<EventTriggerChannel>(persistedTrigger.TriggerChannel);
        Assert.False(persistedEvent.HasSchemaValidation);
        Assert.False(persistedEvent.HasValidation);
        Assert.Null(persistedEvent.Validation);
    }

    [Fact]
    public async Task DomainRepositoryUpdatesSearchesAndPagesDomains()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDomainRepository>();

        var domain = new Domain
        {
            Id = Id.New(),
            Key = "sales",
            DisplayName = "Sales",
            Description = "Original description",
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
        };

        await repository.Upsert(domain);

        domain.DisplayName = "Sales Operations";
        domain.Description = "Commerce orchestration domain";
        domain.IsActive = false;
        await repository.Upsert(domain);

        await repository.Upsert(new Domain
        {
            Id = Id.New(),
            Key = "inventory",
            DisplayName = "Inventory",
            Description = "Stock orchestration domain",
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
        });

        await repository.Upsert(new Domain
        {
            Id = Id.New(),
            Key = "payments",
            DisplayName = "Payments",
            Description = "Payment orchestration domain",
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
        });

        var byId = await repository.GetById(domain.Id);
        var byKey = await repository.GetByKey("sales");
        var search = await repository.GetAll(new PagedSettings(1, 10, [], []), "commerce");
        var secondPage = await repository.GetAll(new PagedSettings(2, 1, [], [new QuerySort("DisplayName", false)]));

        Assert.Equal("Sales Operations", byId.DisplayName);
        Assert.False(byKey.IsActive);
        Assert.Single(search.Rows);
        Assert.Equal("sales", search.Rows.Single().Key);
        Assert.Equal(3, secondPage.TotalRows);
        Assert.Equal(3, secondPage.TotalPages);
        Assert.Equal(2, secondPage.PageNumber);
        Assert.Equal("Payments", secondPage.Rows.Single().DisplayName);
        Assert.Null(await repository.GetByKey(" "));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetById(Id.New()));
    }

    [Fact]
    public async Task TaskRepositoryUpdateReplacesCompleteTaskDefinition()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var definitionRepository = services.GetRequiredService<IOrchestrationDefinitionRepository>();
        var versionRepository = services.GetRequiredService<IOrchestrationVersionRepository>();
        var stageRepository = services.GetRequiredService<IStageRepository>();
        var taskRepository = services.GetRequiredService<ITaskRepository>();

        var definition = new OrchestrationDefinition
        {
            Id = Id.New(),
            Key = "shipping.order.created",
            Name = "Order Created",
            Domain = "shipping",
            DomainId = Id.New(),
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests",
            IsActive = true,
        };
        await definitionRepository.Create(definition);

        var version = Version(definition.Id, 3);
        await versionRepository.Create(version);

        var stage = new StageDefinition
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "shipping",
            Name = "Shipping",
            Order = 1,
        };
        await stageRepository.Create(stage);

        var task = new TaskDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Key = "shipping.prepare",
            Name = "Prepare shipment",
            Order = 1,
            Kind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "shipping.prepare",
                Version = new SemanticVersion(1, 0, 0),
            },
            DispatchType = TaskDispatchType.FireAndWaitCallback,
            IsEnabled = true,
        };
        await taskRepository.Create(task);

        var updatedRequestBinding = SchemaBinding(task.Id, ElementType.Task, SchemaContractKind.CommandRequest, "shipping.dispatch.request");
        var updatedResponseBinding = SchemaBinding(task.Id, ElementType.Task, SchemaContractKind.CommandResponse, "shipping.dispatch.response");
        task.Key = "shipping.dispatch";
        task.Name = "Dispatch shipment";
        task.Order = 5;
        task.ExecutionMode = TaskExecutionMode.Parallel;
        task.OnErrorPolicy = OnErrorPolicy.StopAndCompensate;
        task.DispatchType = TaskDispatchType.FireAndForget;
        task.IsEnabled = false;
        task.Notes = "Updated through repository";
        task.HasExecutionCondition = true;
        task.ExecutionCondition = DslCondition("$trigger.ShouldShip");
        task.HasTransformation = true;
        task.Transformation = DslTransformation("map dispatch request");
        task.Configuration = new MessagingTaskConfiguration
        {
            Topic = "shipping.dispatch",
            Version = new SemanticVersion(3, 1, 0),
            RequestSchemaBinding = updatedRequestBinding,
            ResponseSchemaBinding = updatedResponseBinding,
            HasRequestValidation = true,
            RequestValidation = DslValidation("validate dispatch request", "DispatchRequestInvalid"),
            HasResponseValidation = true,
            ResponseValidation = DslValidation("validate dispatch response", "DispatchResponseInvalid"),
        };
        task.RetryPolicy = RetryPolicy();
        task.TimeoutPolicy = TimeoutFailPolicy();
        task.CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Messaging,
            DispatchType = TaskDispatchType.FireAndWaitCallback,
            HasTransformation = true,
            Transformation = DslTransformation("map dispatch compensation"),
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "shipping.cancel",
                Version = new SemanticVersion(3, 1, 0),
                RequestSchemaBinding = SchemaBinding(task.Id, ElementType.Task, SchemaContractKind.CommandRequest, "shipping.cancel.request"),
            },
            RetryPolicy = RetryPolicy(),
            TimeoutPolicy = TimeoutWaitPolicy(),
        };

        await taskRepository.Update(task);

        var persisted = await taskRepository.GetById(task.Id);
        var persistedConfiguration = Assert.IsType<MessagingTaskConfiguration>(persisted.Configuration);
        var persistedCompensation = Assert.IsType<MessagingTaskConfiguration>(persisted.CompensationDefinition.Configuration);

        Assert.Equal("shipping.dispatch", persisted.Key);
        Assert.Equal("Dispatch shipment", persisted.Name);
        Assert.Equal(5, persisted.Order);
        Assert.Equal(TaskExecutionMode.Parallel, persisted.ExecutionMode);
        Assert.Equal(OnErrorPolicy.StopAndCompensate, persisted.OnErrorPolicy);
        Assert.Equal(TaskDispatchType.FireAndForget, persisted.DispatchType);
        Assert.False(persisted.IsEnabled);
        Assert.True(persisted.HasExecutionCondition);
        Assert.True(persisted.HasTransformation);
        Assert.Equal("shipping.dispatch", persistedConfiguration.Topic);
        Assert.Equal("3.1.0", persistedConfiguration.Version.ToString());
        Assert.Equal("DispatchRequestInvalid", persistedConfiguration.RequestValidation.ErrorCode);
        Assert.Equal("DispatchResponseInvalid", persistedConfiguration.ResponseValidation.ErrorCode);
        Assert.Equal(2, persisted.RetryPolicy.MaxRetries);
        Assert.IsType<FailTimeoutBehaviorPolicy>(persisted.TimeoutPolicy.TimeoutBehaviorPolicy);
        Assert.Equal("shipping.cancel", persistedCompensation.Topic);
        Assert.IsType<WaitTimeoutBehaviorPolicy>(persisted.CompensationDefinition.TimeoutPolicy.TimeoutBehaviorPolicy);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => taskRepository.Update(new TaskDefinition
        {
            Id = Id.New(),
            Key = "missing",
            Name = "Missing",
        }));
    }

    [Fact]
    public async Task DesignRepositoriesUpdateStagesVersionsTriggersAndReadCollections()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        var domainRepository = services.GetRequiredService<IDomainRepository>();
        var definitionRepository = services.GetRequiredService<IOrchestrationDefinitionRepository>();
        var versionRepository = services.GetRequiredService<IOrchestrationVersionRepository>();
        var stageRepository = services.GetRequiredService<IStageRepository>();
        var triggerRepository = services.GetRequiredService<ITriggerBindingRepository>();
        var parallelRepository = services.GetRequiredService<IParallelGroupRepository>();

        var domain = new Domain
        {
            Id = Id.New(),
            Key = "sales",
            DisplayName = "Sales",
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow
        };
        await domainRepository.Upsert(domain);

        var definition = new OrchestrationDefinition
        {
            Id = Id.New(),
            Key = "sales.sale.created",
            Name = "Sale Created",
            Domain = "sales",
            DomainId = domain.Id,
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests"
        };
        await definitionRepository.Create(definition);

        var version = Version(definition.Id, 1);
        var secondVersion = Version(definition.Id, 2);
        await versionRepository.Create(version);
        await versionRepository.Create(secondVersion);

        version.VersionLabel = "v1-updated";
        version.Description = "updated version";
        version.Notes = "notes";
        version.Status = OrchestrationVersionStatus.InReview;
        await versionRepository.Update(version);

        var stage = new StageDefinition
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "inventory",
            Name = "Inventory",
            Description = "original",
            Order = 5,
            HasExecutionCondition = true,
            ExecutionCondition = DslCondition("$trigger.total > 0")
        };
        var secondStage = new StageDefinition
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "payment",
            Name = "Payment",
            Order = 1
        };
        await stageRepository.Create(stage);
        await stageRepository.Create(secondStage);

        stage.Key = "inventory.updated";
        stage.Name = "Inventory Updated";
        stage.Description = "updated";
        stage.Order = 0;
        stage.ExecutionCondition = DslCondition("$trigger.canReserve");
        await stageRepository.Update(stage);

        var parallelGroup = new ParallelGroupDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Name = "Group A",
            JoinPolicy = ParallelJoinPolicy.WaitAll,
            MaxParallelAgents = 3
        };
        await parallelRepository.Create(parallelGroup);
        parallelGroup.Name = "Group Updated";
        parallelGroup.JoinPolicy = ParallelJoinPolicy.WaitAll;
        parallelGroup.MaxParallelAgents = null;
        await parallelRepository.Update(parallelGroup);

        var trigger = new TriggerBinding
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "sale-created",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            Description = "original",
            TriggerChannel = new EventTriggerChannel
            {
                Topic = "events.sales.sale.created",
                Version = new SemanticVersion(1, 0, 0)
            }
        };
        await triggerRepository.Create(trigger);

        trigger.Key = "sale-created-updated";
        trigger.Description = "updated";
        trigger.IsEnabled = false;
        trigger.TriggerChannel = new EventTriggerChannel
        {
            Topic = "events.sales.sale.created.v2",
            Version = new SemanticVersion(2, 0, 0),
            HasValidation = true,
            Validation = DslValidation("validate trigger", "TriggerInvalid")
        };
        await triggerRepository.Update(trigger);

        var persistedVersion = await versionRepository.GetById(version.Id);
        var versionPage = await versionRepository.GetAll(definition.Id, new PagedSettings(1, 10, [], []));
        var persistedStage = await stageRepository.GetById(stage.Id);
        var stages = (await stageRepository.GetAll(version.Id)).ToArray();
        var persistedGroup = await parallelRepository.GetById(parallelGroup.Id);
        var groups = (await parallelRepository.GetAll(stage.Id)).ToArray();
        var persistedTrigger = await triggerRepository.GetById(trigger.Id);
        var triggers = (await triggerRepository.GetAll(version.Id)).ToArray();

        Assert.Equal("v1-updated", persistedVersion.VersionLabel);
        Assert.Equal(2, versionPage.TotalRows);
        Assert.Equal("inventory.updated", persistedStage.Key);
        Assert.Equal([stage.Id, secondStage.Id], stages.Select(item => item.Id).ToArray());
        Assert.Equal("Group Updated", persistedGroup.Name);
        Assert.Null(persistedGroup.MaxParallelAgents);
        Assert.Single(groups);
        Assert.Equal("sale-created-updated", persistedTrigger.Key);
        Assert.False(persistedTrigger.IsEnabled);
        Assert.Single(triggers);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => versionRepository.Update(Version(definition.Id, 9)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => stageRepository.Update(new StageDefinition
        {
            Id = Id.New(),
            Key = "missing",
            Name = "Missing"
        }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => triggerRepository.Update(new TriggerBinding
        {
            Id = Id.New(),
            Key = "missing",
            TriggerChannel = new EventTriggerChannel { Topic = "missing", Version = new SemanticVersion(1, 0, 0) }
        }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => parallelRepository.Update(new ParallelGroupDefinition { Id = Id.New() }));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<Sieve.Models.SieveOptions>(_ => { });
        services.AddOrchestratorControlPlaneStorageEntityFramework(options =>
            options.UseInMemoryDatabase($"control-plane-design-{Guid.NewGuid():N}"));
        return services.BuildServiceProvider();
    }

    private static OrchestrationVersion Version(Id definitionId, int major)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionId = definitionId,
            Version = new SemanticVersion(major, 0, 0),
            Status = OrchestrationVersionStatus.Draft,
            VersionLabel = $"{major}.0.0",
            Checksum = new Checksum($"sha256:{major}"),
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests",
        };

    private static ExecutionCondition DslCondition(string expression)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression(expression) },
        };

    private static TransformationDefinition DslTransformation(string dsl)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration
            {
                Dsl = dsl,
                SourceContextHash = "source",
                TargetSchemaHash = "target",
                SemanticDiagnosticsJson = """{"valid":true}""",
            },
        };

    private static ValidationDefinition DslValidation(string dsl, string errorCode)
        => new()
        {
            Engine = EngineType.DSL,
            ErrorCode = errorCode,
            Configuration = new DslValidationConfiguration
            {
                Dsl = dsl,
                SchemaHash = "schema",
                SemanticDiagnosticsJson = """{"valid":true}""",
            },
        };

    private static RetryPolicy RetryPolicy()
        => new()
        {
            MaxRetries = 2,
            StrategyType = RetryStrategyType.Fixed,
            Strategy = new FixedRetryStrategy { Delay = Duration.FromSeconds(3) },
            RetryableErrorCodes = ["Timeout", "Transient"],
            StopOnNonRetryableError = true,
        };

    private static TimeoutPolicy TimeoutFailPolicy()
        => new()
        {
            Timeout = Duration.FromMinutes(5),
            TimeoutBehavior = TimeoutBehavior.Fail,
            TimeoutBehaviorPolicy = new FailTimeoutBehaviorPolicy { ErrorCode = "TaskTimeout" },
        };

    private static TimeoutPolicy TimeoutWaitPolicy()
        => new()
        {
            Timeout = Duration.FromMinutes(1),
            TimeoutBehavior = TimeoutBehavior.Wait,
            TimeoutBehaviorPolicy = new WaitTimeoutBehaviorPolicy
            {
                OrchestrationAction = OrchestrationActionOnTimeout.Block,
                WaitingTime = Duration.FromSeconds(30),
            },
        };

    private static TimeoutPolicy TimeoutReconcilePolicy()
        => new()
        {
            Timeout = Duration.FromMinutes(2),
            TimeoutBehavior = TimeoutBehavior.Reconcile,
            TimeoutBehaviorPolicy = new ReconcileTimeoutBehaviorPolicy
            {
                OrchestrationAction = OrchestrationActionOnTimeout.Continue,
                RetryPolicy = RetryPolicy(),
            },
        };

    private static SchemaBinding SchemaBinding(
        Id? messagingTaskId,
        ElementType elementType,
        SchemaContractKind contractKind,
        string contractKey)
        => new()
        {
            Id = Id.New(),
            ElementType = elementType,
            ElementId = messagingTaskId ?? Id.New(),
            ContractId = Id.New(),
            ContractKey = contractKey,
            ContractVersion = new SemanticVersion(1, 0, 0),
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "knowl",
            ContractKind = contractKind,
            StrictMode = true,
            IsValidationEnabled = true,
            Snapshot = new DesignSchemaContractSnapshot
            {
                ContractKind = contractKind,
                RegistryProviderId = "knowl",
                RegistryProviderKey = "knowl",
                ContractId = contractKey,
                ContractKey = contractKey,
                ContractVersion = "1.0.0",
                SchemaFormat = "ButterMorph",
                SchemaJson = """{"type":"object"}""",
                ContentHash = $"hash-{contractKey}",
                SourceArtifactId = $"artifact-{contractKey}",
                ResolvedBy = "tests",
                ResolvedAtUtc = DateTimeOffset.UtcNow,
            },
        };
}
