namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

public sealed class ControlPlaneDbContextNormalizationTests
{
    [Fact]
    public async Task SaveChangesAsync_NormalizesMissingPolymorphicDiscriminators()
    {
        await using var dbContext = CreateContext();
        var stageId = Id.New();
        var versionId = Id.New();
        var taskId = Id.New();
        var stage = new StageDefinitionEntity
        {
            Id = stageId,
            OrchestrationVersionId = versionId,
            Key = "fulfillment",
            Name = "Fulfillment",
            Order = 1,
            ExecutionCondition = Condition()
        };
        var branch = new BranchRuleDefinitionEntity
        {
            Id = Id.New(),
            StageDefinitionId = stageId,
            FromId = stageId,
            FromType = ElementType.Stage,
            NavigateToId = taskId,
            NavigateToType = ElementType.Task,
            Condition = Condition()
        };
        var messagingTask = Task(taskId, stageId, TaskKind.Messaging, TaskConfiguration(
            messaging: new MessagingTaskConfigurationJsonModel
            {
                Topic = "inventories.reserve",
                Version = "1.0.0",
                RequestValidation = Validation(),
                ResponseValidation = Validation()
            }));
        messagingTask.ExecutionCondition = Condition();
        messagingTask.Transformation = Transformation();
        messagingTask.RetryPolicy = new RetryPolicyJsonModel
        {
            Strategy = new RetryStrategyEnvelopeJsonModel()
        };
        messagingTask.TimeoutPolicy = new TimeoutPolicyJsonModel
        {
            TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel
            {
                Fail = new FailTimeoutBehaviorPolicyJsonModel()
            }
        };
        messagingTask.CompensationDefinition = new CompensationDefinitionJsonModel
        {
            CompensationTaskKind = TaskKind.Http,
            DispatchType = TaskDispatchType.FireAndWait,
            ExecutionCondition = Condition(),
            Transformation = Transformation(),
            Configuration = TaskConfiguration(http: new HttpTaskConfigurationJsonModel()),
            RetryPolicy = new RetryPolicyJsonModel { Strategy = new RetryStrategyEnvelopeJsonModel() },
            TimeoutPolicy = new TimeoutPolicyJsonModel
            {
                TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel
                {
                    Wait = new WaitTimeoutBehaviorPolicyJsonModel()
                }
            }
        };
        var httpTask = Task(Id.New(), stageId, TaskKind.Http, TaskConfiguration(http: new HttpTaskConfigurationJsonModel()));
        httpTask.TimeoutPolicy = new TimeoutPolicyJsonModel
        {
            TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel
            {
                Reconcile = new ReconcileTimeoutBehaviorPolicyJsonModel()
            }
        };
        var pluginTask = Task(Id.New(), stageId, TaskKind.Plugin, TaskConfiguration(plugin: new PluginTaskConfigurationJsonModel()));
        var humanTask = Task(Id.New(), stageId, TaskKind.HumanApproval, TaskConfiguration(humanApproval: new HumanApprovalTaskConfigurationJsonModel()));
        var trigger = new TriggerBindingEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = "sale-created",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new TriggerChannelEnvelopeJsonModel
            {
                Event = new EventTriggerChannelJsonModel
                {
                    Topic = "events.sales.sale.created",
                    Version = "1.0.0",
                    Validation = Validation()
                }
            }
        };

        dbContext.StageDefinitions.Add(stage);
        dbContext.BranchRuleDefinitions.Add(branch);
        dbContext.TaskDefinitions.AddRange(messagingTask, httpTask, pluginTask, humanTask);
        dbContext.TriggerBindings.Add(trigger);

        await dbContext.SaveChangesAsync();

        Assert.Equal("dsl", stage.ExecutionCondition.Configuration.Type);
        Assert.Equal("dsl", branch.Condition.Configuration.Type);
        Assert.Equal("messaging", messagingTask.Configuration.Type);
        Assert.Equal("http", httpTask.Configuration.Type);
        Assert.Equal("plugin", pluginTask.Configuration.Type);
        Assert.Equal("humanApproval", humanTask.Configuration.Type);
        Assert.Equal("dsl", messagingTask.ExecutionCondition.Configuration.Type);
        Assert.Equal("dsl", messagingTask.Transformation.Configuration.Type);
        Assert.Equal("fixed", messagingTask.RetryPolicy.Strategy.Type);
        Assert.Equal("fail", messagingTask.TimeoutPolicy.TimeoutBehaviorPolicy.Type);
        Assert.Equal("http", messagingTask.CompensationDefinition.Configuration.Type);
        Assert.Equal("dsl", messagingTask.CompensationDefinition.ExecutionCondition.Configuration.Type);
        Assert.Equal("dsl", messagingTask.CompensationDefinition.Transformation.Configuration.Type);
        Assert.Equal("fixed", messagingTask.CompensationDefinition.RetryPolicy.Strategy.Type);
        Assert.Equal("wait", messagingTask.CompensationDefinition.TimeoutPolicy.TimeoutBehaviorPolicy.Type);
        Assert.Equal("reconcile", httpTask.TimeoutPolicy.TimeoutBehaviorPolicy.Type);
        Assert.Equal("event", trigger.TriggerChannel.Type);
        Assert.Equal("dsl", trigger.TriggerChannel.Event.Validation.Configuration.Type);
        Assert.Equal("dsl", messagingTask.Configuration.Messaging.RequestValidation.Configuration.Type);
        Assert.Equal("dsl", messagingTask.Configuration.Messaging.ResponseValidation.Configuration.Type);
    }

    [Fact]
    public void SaveChanges_NormalizesSynchronousSaves()
    {
        using var dbContext = CreateContext();
        var stage = new StageDefinitionEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = Id.New(),
            Key = "capture",
            Name = "Capture",
            Order = 1,
            ExecutionCondition = Condition()
        };

        dbContext.StageDefinitions.Add(stage);
        dbContext.SaveChanges();

        Assert.Equal("dsl", stage.ExecutionCondition.Configuration.Type);
    }

    private static ControlPlaneDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseInMemoryDatabase($"control-plane-normalization-{Guid.NewGuid():N}")
            .Options);

    private static TaskDefinitionEntity Task(
        Id id,
        Id stageId,
        TaskKind kind,
        TaskConfigurationEnvelopeJsonModel configuration)
        => new()
        {
            Id = id,
            StageDefinitionId = stageId,
            Key = $"{kind}.task",
            Name = $"{kind} task",
            Order = 1,
            Kind = kind,
            ExecutionMode = TaskExecutionMode.Sequential,
            DispatchType = TaskDispatchType.FireAndForget,
            IsEnabled = true,
            Configuration = configuration
        };

    private static ExecutionConditionJsonModel Condition()
        => new()
        {
            IsEnabled = true,
            Engine = EngineType.DSL,
            Configuration = new ConditionConfigurationEnvelopeJsonModel
            {
                Dsl = new DslConditionConfigurationJsonModel { Expression = "$trigger.Enabled" }
            }
        };

    private static TransformationDefinitionJsonModel Transformation()
        => new()
        {
            IsEnabled = true,
            Engine = EngineType.DSL,
            Configuration = new TransformationConfigurationEnvelopeJsonModel
            {
                Dsl = new DslTransformationConfigurationJsonModel
                {
                    Dsl = "map {}",
                    SourceContextHash = "source",
                    TargetSchemaHash = "target",
                    SemanticDiagnosticsJson = "{}"
                }
            }
        };

    private static ValidationDefinitionJsonModel Validation()
        => new()
        {
            IsEnabled = true,
            Engine = EngineType.DSL,
            ErrorCode = "ValidationFailed",
            Configuration = new ValidationConfigurationEnvelopeJsonModel
            {
                Dsl = new DslValidationConfigurationJsonModel
                {
                    Dsl = "validate {}",
                    SchemaHash = "schema",
                    SemanticDiagnosticsJson = "{}"
                }
            }
        };

    private static TaskConfigurationEnvelopeJsonModel TaskConfiguration(
        HttpTaskConfigurationJsonModel? http = null,
        MessagingTaskConfigurationJsonModel? messaging = null,
        PluginTaskConfigurationJsonModel? plugin = null,
        HumanApprovalTaskConfigurationJsonModel? humanApproval = null)
        => new()
        {
            Http = http,
            Messaging = messaging,
            Plugin = plugin,
            HumanApproval = humanApproval
        };
}
