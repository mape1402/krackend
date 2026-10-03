namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

public sealed class ControlPlaneDbContextNormalizationTests
{
    [Fact]
    public void ConditionConfigurationEnvelopeHonorsLegacySettersOnlyWhenEmpty()
    {
        var dsl = new DslConditionConfigurationJsonModel { Expression = "$trigger.Enabled" };
        var replacement = new DslConditionConfigurationJsonModel { Expression = "$trigger.Disabled" };
        var legacyDsl = new DslConditionConfigurationJsonModel
        {
            LegacyExpression = "$trigger.Legacy"
        };
        legacyDsl.LegacyExpression = "$trigger.Ignored";
        var envelope = new ConditionConfigurationEnvelopeJsonModel
        {
            LegacyType = null!,
            DslLegacy = null!
        };

        Assert.Equal(string.Empty, envelope.Type);
        Assert.Null(envelope.Dsl);

        envelope.LegacyType = "dsl";
        envelope.DslLegacy = dsl;
        envelope.LegacyType = "ignored";
        envelope.DslLegacy = replacement;

        Assert.Equal("dsl", envelope.Type);
        Assert.Same(dsl, envelope.Dsl);
        Assert.Equal("$trigger.Legacy", legacyDsl.Expression);
    }

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

    [Fact]
    public async Task SaveChangesAsync_HandlesNullAndExistingPolymorphicBranches()
    {
        await using var dbContext = CreateContext();
        _ = dbContext.RuntimeCapabilities;
        var stageId = Id.New();
        var versionId = Id.New();
        var stageWithoutCondition = new StageDefinitionEntity
        {
            Id = stageId,
            OrchestrationVersionId = versionId,
            Key = "stage-without-condition",
            Name = "Stage without condition",
            Order = 1
        };
        var stageWithExistingCondition = new StageDefinitionEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = "stage-existing-condition",
            Name = "Stage existing condition",
            Order = 2,
            ExecutionCondition = Condition()
        };
        stageWithExistingCondition.ExecutionCondition.Configuration.Type = "custom-condition";
        var stageWithMissingConditionConfiguration = new StageDefinitionEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = "stage-missing-condition-config",
            Name = "Stage missing condition config",
            Order = 3,
            ExecutionCondition = new ExecutionConditionJsonModel()
        };

        var branchWithoutCondition = new BranchRuleDefinitionEntity
        {
            Id = Id.New(),
            StageDefinitionId = stageId,
            FromId = stageId,
            FromType = ElementType.Stage,
            NavigateToId = Id.New(),
            NavigateToType = ElementType.Stage
        };
        var branchWithMissingConditionConfiguration = new BranchRuleDefinitionEntity
        {
            Id = Id.New(),
            StageDefinitionId = stageId,
            FromId = stageId,
            FromType = ElementType.Stage,
            NavigateToId = Id.New(),
            NavigateToType = ElementType.Stage,
            Condition = new ExecutionConditionJsonModel()
        };
        var taskWithoutConfiguration = Task(Id.New(), stageId, TaskKind.Http, null!);
        var taskWithExistingConfiguration = Task(
            Id.New(),
            stageId,
            TaskKind.Messaging,
            new TaskConfigurationEnvelopeJsonModel
            {
                Type = "custom-task",
                Messaging = new MessagingTaskConfigurationJsonModel
                {
                    RequestValidation = new ValidationDefinitionJsonModel(),
                    ResponseValidation = new ValidationDefinitionJsonModel
                    {
                        Configuration = new ValidationConfigurationEnvelopeJsonModel
                        {
                            Type = "custom-validation"
                        }
                    }
                }
            });
        var taskWithDefaultTimeoutBehavior = Task(
            Id.New(),
            stageId,
            TaskKind.HumanApproval,
            TaskConfiguration(humanApproval: new HumanApprovalTaskConfigurationJsonModel()));
        taskWithDefaultTimeoutBehavior.TimeoutPolicy = new TimeoutPolicyJsonModel
        {
            TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel()
        };
        var compensationWithExistingTypes = Task(
            Id.New(),
            stageId,
            TaskKind.Plugin,
            TaskConfiguration(plugin: new PluginTaskConfigurationJsonModel()));
        compensationWithExistingTypes.CompensationDefinition = new CompensationDefinitionJsonModel
        {
            CompensationTaskKind = TaskKind.Messaging,
            Configuration = new TaskConfigurationEnvelopeJsonModel
            {
                Type = "custom-compensation",
                Messaging = new MessagingTaskConfigurationJsonModel
                {
                    RequestValidation = Validation(),
                    ResponseValidation = Validation()
                }
            },
            ExecutionCondition = Condition(),
            Transformation = Transformation(),
            RetryPolicy = new RetryPolicyJsonModel
            {
                Strategy = new RetryStrategyEnvelopeJsonModel { Type = "custom-retry" }
            },
            TimeoutPolicy = new TimeoutPolicyJsonModel
            {
                TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel { Type = "custom-timeout" }
            }
        };
        compensationWithExistingTypes.CompensationDefinition.ExecutionCondition.Configuration.Type = "custom-condition";
        compensationWithExistingTypes.CompensationDefinition.Transformation.Configuration.Type = "custom-transform";
        var compensationWithMissingNestedConfigurations = Task(
            Id.New(),
            stageId,
            TaskKind.Messaging,
            TaskConfiguration(messaging: new MessagingTaskConfigurationJsonModel()));
        compensationWithMissingNestedConfigurations.CompensationDefinition = new CompensationDefinitionJsonModel
        {
            CompensationTaskKind = TaskKind.Messaging,
            Configuration = TaskConfiguration(messaging: new MessagingTaskConfigurationJsonModel
            {
                RequestValidation = new ValidationDefinitionJsonModel
                {
                    Configuration = new ValidationConfigurationEnvelopeJsonModel()
                },
                ResponseValidation = new ValidationDefinitionJsonModel
                {
                    Configuration = new ValidationConfigurationEnvelopeJsonModel()
                }
            }),
            ExecutionCondition = new ExecutionConditionJsonModel(),
            Transformation = new TransformationDefinitionJsonModel(),
            RetryPolicy = new RetryPolicyJsonModel
            {
                Strategy = new RetryStrategyEnvelopeJsonModel()
            },
            TimeoutPolicy = new TimeoutPolicyJsonModel
            {
                TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel()
            }
        };

        var triggerWithoutChannel = new TriggerBindingEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = "trigger-without-channel",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = null!
        };
        var triggerWithExistingChannel = new TriggerBindingEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = "trigger-existing-channel",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new TriggerChannelEnvelopeJsonModel
            {
                Type = "custom-trigger",
                Event = new EventTriggerChannelJsonModel
                {
                    Validation = new ValidationDefinitionJsonModel()
                }
            }
        };
        var triggerWithMissingValidationConfiguration = new TriggerBindingEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = "trigger-missing-validation-config",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new TriggerChannelEnvelopeJsonModel
            {
                Event = new EventTriggerChannelJsonModel
                {
                    Validation = new ValidationDefinitionJsonModel()
                }
            }
        };
        var triggerWithoutEventChannelPayload = new TriggerBindingEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = "trigger-without-event-payload",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new TriggerChannelEnvelopeJsonModel
            {
                Event = null
            }
        };
        var triggerWithoutValidation = new TriggerBindingEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = "trigger-without-validation",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new TriggerChannelEnvelopeJsonModel
            {
                Event = new EventTriggerChannelJsonModel
                {
                    Validation = null
                }
            }
        };
        var triggerWithEmptyValidationConfiguration = new TriggerBindingEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = "trigger-empty-validation-config",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new TriggerChannelEnvelopeJsonModel
            {
                Event = new EventTriggerChannelJsonModel
                {
                    Validation = new ValidationDefinitionJsonModel
                    {
                        Configuration = new ValidationConfigurationEnvelopeJsonModel()
                    }
                }
            }
        };

        dbContext.StageDefinitions.AddRange(stageWithoutCondition, stageWithExistingCondition, stageWithMissingConditionConfiguration);
        dbContext.BranchRuleDefinitions.AddRange(branchWithoutCondition, branchWithMissingConditionConfiguration);
        dbContext.TaskDefinitions.AddRange(
            taskWithoutConfiguration,
            taskWithExistingConfiguration,
            taskWithDefaultTimeoutBehavior,
            compensationWithExistingTypes,
            compensationWithMissingNestedConfigurations);
        dbContext.TriggerBindings.AddRange(
            triggerWithoutChannel,
            triggerWithExistingChannel,
            triggerWithMissingValidationConfiguration,
            triggerWithoutEventChannelPayload,
            triggerWithoutValidation,
            triggerWithEmptyValidationConfiguration);

        await dbContext.SaveChangesAsync();

        Assert.Equal("custom-condition", stageWithExistingCondition.ExecutionCondition.Configuration.Type);
        Assert.Null(stageWithMissingConditionConfiguration.ExecutionCondition.Configuration);
        Assert.Null(branchWithMissingConditionConfiguration.Condition.Configuration);
        Assert.Null(taskWithoutConfiguration.Configuration);
        Assert.Equal("custom-task", taskWithExistingConfiguration.Configuration.Type);
        Assert.Null(taskWithExistingConfiguration.Configuration.Messaging.RequestValidation.Configuration);
        Assert.Equal("custom-validation", taskWithExistingConfiguration.Configuration.Messaging.ResponseValidation.Configuration.Type);
        Assert.Equal("fail", taskWithDefaultTimeoutBehavior.TimeoutPolicy.TimeoutBehaviorPolicy.Type);
        Assert.Equal("custom-compensation", compensationWithExistingTypes.CompensationDefinition.Configuration.Type);
        Assert.Equal("custom-condition", compensationWithExistingTypes.CompensationDefinition.ExecutionCondition.Configuration.Type);
        Assert.Equal("custom-transform", compensationWithExistingTypes.CompensationDefinition.Transformation.Configuration.Type);
        Assert.Equal("custom-retry", compensationWithExistingTypes.CompensationDefinition.RetryPolicy.Strategy.Type);
        Assert.Equal("custom-timeout", compensationWithExistingTypes.CompensationDefinition.TimeoutPolicy.TimeoutBehaviorPolicy.Type);
        Assert.Equal("messaging", compensationWithMissingNestedConfigurations.CompensationDefinition.Configuration.Type);
        Assert.Null(compensationWithMissingNestedConfigurations.CompensationDefinition.ExecutionCondition.Configuration);
        Assert.Null(compensationWithMissingNestedConfigurations.CompensationDefinition.Transformation.Configuration);
        Assert.Equal("fixed", compensationWithMissingNestedConfigurations.CompensationDefinition.RetryPolicy.Strategy.Type);
        Assert.Equal("fail", compensationWithMissingNestedConfigurations.CompensationDefinition.TimeoutPolicy.TimeoutBehaviorPolicy.Type);
        Assert.Equal("dsl", compensationWithMissingNestedConfigurations.CompensationDefinition.Configuration.Messaging.RequestValidation.Configuration.Type);
        Assert.Equal("dsl", compensationWithMissingNestedConfigurations.CompensationDefinition.Configuration.Messaging.ResponseValidation.Configuration.Type);
        Assert.Null(triggerWithoutChannel.TriggerChannel);
        Assert.Equal("custom-trigger", triggerWithExistingChannel.TriggerChannel.Type);
        Assert.Null(triggerWithExistingChannel.TriggerChannel.Event.Validation.Configuration);
        Assert.Null(triggerWithMissingValidationConfiguration.TriggerChannel.Event.Validation.Configuration);
        Assert.Equal("event", triggerWithoutEventChannelPayload.TriggerChannel.Type);
        Assert.Null(triggerWithoutEventChannelPayload.TriggerChannel.Event);
        Assert.Equal("event", triggerWithoutValidation.TriggerChannel.Type);
        Assert.Null(triggerWithoutValidation.TriggerChannel.Event.Validation);
        Assert.Equal("dsl", triggerWithEmptyValidationConfiguration.TriggerChannel.Event.Validation.Configuration.Type);
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
