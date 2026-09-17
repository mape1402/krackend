namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.SchemaRegistry;

public sealed class MessagingRuntimeArtifactCompatibilityValidatorTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemanticVersion Version = new(1, 0, 0);

    [Fact]
    public async Task ValidateAsync_WhenPayloadIsNull_ReturnsPayloadMissing()
    {
        var result = await new MessagingRuntimeArtifactCompatibilityValidator().ValidateAsync(null!);

        Assert.False(result.Succeeded);
        Assert.Equal("ArtifactPayloadMissing", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenPayloadCannotBeDeserialized_ReturnsNotDeserializable()
    {
        var result = await new MessagingRuntimeArtifactCompatibilityValidator()
            .ValidateAsync(JsonNode.Parse("""{"version":"not-semver"}""")!);

        Assert.False(result.Succeeded);
        Assert.Equal("ArtifactPayloadNotDeserializable", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenPayloadIsJsonNull_ReturnsPayloadMissing()
    {
        using var document = JsonDocument.Parse("null");
        var payload = JsonValue.Create(document.RootElement.Clone())!;

        var result = await new MessagingRuntimeArtifactCompatibilityValidator().ValidateAsync(payload);

        Assert.False(result.Succeeded);
        Assert.Equal("ArtifactPayloadMissing", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenArtifactUsesSupportedMessagingShape_ReturnsSuccess()
    {
        var result = await ValidateAsync(CreateArtifact());

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenEnabledDslPipelinesAndPoliciesAreSupported_ReturnsSuccess()
    {
        var requestBinding = SchemaBinding(validationEnabled: true);
        var responseBinding = SchemaBinding(validationEnabled: true);
        var task = MessagingTask("inventories.reserve") with
        {
            ExecutionCondition = EnabledCondition("$trigger.Total > 0"),
            Transformation = EnabledTransformation("map reserve request"),
            Configuration = MessagingConfiguration(
                "commands.inventories.reserve",
                requestBinding,
                responseBinding) with
            {
                RequestValidation = DslValidation("validate request"),
                ResponseValidation = DslValidation("validate response")
            },
            RetryPolicy = new RetryPolicyArtifact(
                3,
                RetryStrategyType.Fixed,
                new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
                ["InventoryTransient"],
                true),
            TimeoutPolicy = new TimeoutPolicyArtifact(
                Duration.FromSeconds(30),
                TimeoutBehavior.Fail,
                new FailTimeoutBehaviorPolicyArtifact("InventoryTimeout"))
        };
        var trigger = EventTrigger("events.sales.sale.created") with
        {
            TriggerChannel = EventTriggerChannel("events.sales.sale.created") with
            {
                SchemaBinding = SchemaBinding(validationEnabled: true),
                Validation = DslValidation("validate trigger")
            }
        };
        var artifact = CreateArtifact(
            triggers: [trigger],
            stages:
            [
                Stage(
                    "stage-one",
                    1,
                    condition: EnabledCondition("$trigger.Valid == true"),
                    tasks: [task])
            ]);

        var result = await ValidateAsync(artifact);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenWaitTimeoutPolicyIsValid_ReturnsSuccess()
    {
        var task = MessagingTask("task-one") with
        {
            TimeoutPolicy = new TimeoutPolicyArtifact(
                Duration.FromSeconds(30),
                TimeoutBehavior.Wait,
                new WaitTimeoutBehaviorPolicyArtifact(
                    OrchestrationActionOnTimeout.Block,
                    Duration.FromSeconds(10)))
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact]
    public void ValidateTimeoutPolicy_WhenBehaviorPayloadIsUnsupported_ReturnsFailure()
    {
        var method = typeof(MessagingRuntimeArtifactCompatibilityValidator).GetMethod(
            "ValidateTimeoutPolicy",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var result = (RuntimeArtifactCompatibilityValidationResult)method.Invoke(
            null,
            [
                new TimeoutPolicyArtifact(
                    Duration.FromSeconds(30),
                    TimeoutBehavior.Fail,
                    new UnsupportedTimeoutBehaviorPolicyArtifact()),
                "task 'unsupported'"
            ])!;

        AssertFailure(result, "TimeoutBehaviorNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenTriggerIsNotEvent_ReturnsTriggerTransportNotSupported()
    {
        var artifact = CreateArtifact(triggers:
        [
            new TriggerBindingArtifact(Id.New(), TriggerType.Manual, EventTriggerChannel("events.sales.sale.created"), true)
        ]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "TriggerTransportNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenTriggerTopicIsMissing_ReturnsTriggerTopicMissing()
    {
        var artifact = CreateArtifact(triggers: [EventTrigger(string.Empty)]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "TriggerTopicMissing");
    }

    [Fact]
    public async Task ValidateAsync_WhenTriggerSchemaValidationHasNoDsl_ReturnsValidationDslMissing()
    {
        var channel = EventTriggerChannel("events.sales.sale.created") with
        {
            SchemaBinding = SchemaBinding(validationEnabled: true)
        };
        var artifact = CreateArtifact(triggers: [new TriggerBindingArtifact(Id.New(), TriggerType.Event, channel, true)]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "ValidationDslMissing");
    }

    [Fact]
    public async Task ValidateAsync_WhenTriggerValidationUsesUnsupportedEngine_ReturnsValidationEngineNotSupported()
    {
        var channel = EventTriggerChannel("events.sales.sale.created") with
        {
            Validation = new ValidationArtifact(
                EngineType.Plugin,
                new DslValidationConfigurationArtifact { Dsl = "source {}" })
            {
                IsEnabled = true
            }
        };
        var artifact = CreateArtifact(triggers: [new TriggerBindingArtifact(Id.New(), TriggerType.Event, channel, true)]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "ValidationEngineNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenStageConditionUsesUnsupportedEngine_ReturnsConditionEngineNotSupported()
    {
        var artifact = CreateArtifact(stages:
        [
            Stage("stage-one", 1, condition: EnabledCondition("true", EngineType.Plugin), tasks: [MessagingTask("task-one")])
        ]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "ConditionEngineNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenTaskKindIsNotMessaging_ReturnsTaskKindNotSupported()
    {
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [HttpTask("task-http")])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "TaskKindNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenTaskConfigurationIsNotMessaging_ReturnsTaskConfigurationNotSupported()
    {
        var task = MessagingTask("task-one") with
        {
            Configuration = new HumanApprovalTaskConfigurationArtifact()
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "TaskConfigurationNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenMessagingTopicIsMissing_ReturnsMessagingTopicMissing()
    {
        var task = MessagingTask("task-one") with
        {
            Configuration = MessagingConfiguration(string.Empty)
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "MessagingTopicMissing");
    }

    [Fact]
    public async Task ValidateAsync_WhenMessagingDispatchIsSynchronous_ReturnsDispatchNotSupported()
    {
        var task = MessagingTask("task-one") with { DispatchType = TaskDispatchType.FireAndWait };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "MessagingDispatchTypeNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenRetryCountIsNegative_ReturnsRetryMaxRetriesInvalid()
    {
        var task = MessagingTask("task-one") with
        {
            RetryPolicy = new RetryPolicyArtifact(
                -1,
                RetryStrategyType.Fixed,
                new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
                [],
                true)
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "RetryMaxRetriesInvalid");
    }

    [Fact]
    public async Task ValidateAsync_WhenRetryDelayIsNegative_ReturnsRetryStrategyNotSupported()
    {
        var task = MessagingTask("task-one") with
        {
            RetryPolicy = new RetryPolicyArtifact(
                1,
                RetryStrategyType.Fixed,
                new FixedRetryStrategyArtifact(new Duration(TimeSpan.FromSeconds(-1))),
                [],
                true)
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "RetryStrategyNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenTimeoutIsZero_ReturnsTimeoutDurationInvalid()
    {
        var task = MessagingTask("task-one") with
        {
            TimeoutPolicy = new TimeoutPolicyArtifact(
                Duration.FromSeconds(0),
                TimeoutBehavior.Fail,
                new FailTimeoutBehaviorPolicyArtifact("TIMEOUT"))
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "TimeoutDurationInvalid");
    }

    [Fact]
    public async Task ValidateAsync_WhenTimeoutBehaviorPolicyIsMissing_ReturnsTimeoutBehaviorMismatch()
    {
        var task = MessagingTask("task-one") with
        {
            TimeoutPolicy = new TimeoutPolicyArtifact(Duration.FromSeconds(30), TimeoutBehavior.Fail, null!)
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "TimeoutBehaviorMismatch");
    }

    [Fact]
    public async Task ValidateAsync_WhenWaitTimeoutDurationIsInvalid_ReturnsTimeoutWaitDurationInvalid()
    {
        var task = MessagingTask("task-one") with
        {
            TimeoutPolicy = new TimeoutPolicyArtifact(
                Duration.FromSeconds(30),
                TimeoutBehavior.Wait,
                new WaitTimeoutBehaviorPolicyArtifact(
                    OrchestrationActionOnTimeout.Block,
                    Duration.FromSeconds(0)))
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "TimeoutWaitDurationInvalid");
    }

    [Fact]
    public async Task ValidateAsync_WhenReconcileRetryStrategyIsUnsupported_ReturnsRetryStrategyNotSupported()
    {
        var task = MessagingTask("task-one") with
        {
            TimeoutPolicy = new TimeoutPolicyArtifact(
                Duration.FromSeconds(30),
                TimeoutBehavior.Reconcile,
                new ReconcileTimeoutBehaviorPolicyArtifact(
                    OrchestrationActionOnTimeout.Block,
                    new RetryPolicyArtifact(
                        1,
                        RetryStrategyType.Exponential,
                        new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
                        [],
                        true)))
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "RetryStrategyNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenTaskConditionExpressionIsMissing_ReturnsConditionExpressionMissing()
    {
        var task = MessagingTask("task-one") with
        {
            ExecutionCondition = EnabledCondition(string.Empty)
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "ConditionExpressionMissing");
    }

    [Fact]
    public async Task ValidateAsync_WhenTaskTransformationUsesUnsupportedEngine_ReturnsTransformationEngineNotSupported()
    {
        var task = MessagingTask("task-one") with
        {
            Transformation = EnabledTransformation("target {}", EngineType.Plugin)
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "TransformationEngineNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenResponseSchemaValidationHasNoDsl_ReturnsValidationDslMissing()
    {
        var task = MessagingTask("task-one") with
        {
            Configuration = MessagingConfiguration(
                "commands.sales.sale.reserve",
                responseBinding: SchemaBinding(validationEnabled: true))
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "ValidationDslMissing");
    }

    [Fact]
    public async Task ValidateAsync_WhenCompensationKindIsUnsupported_ReturnsCompensationTaskKindNotSupported()
    {
        var task = MessagingTask("task-one") with
        {
            Compensation = Compensation("commands.sales.undo", TaskKind.Http, TaskDispatchType.FireAndForget)
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "CompensationTaskKindNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenCompensationConfigurationIsNotMessaging_ReturnsCompensationConfigurationNotSupported()
    {
        var compensation = Compensation("commands.sales.undo", TaskKind.Messaging, TaskDispatchType.FireAndForget) with
        {
            Configuration = new HumanApprovalTaskConfigurationArtifact()
        };
        var task = MessagingTask("task-one") with { Compensation = compensation };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "CompensationConfigurationNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenCompensationTopicIsMissing_ReturnsCompensationMessagingTopicMissing()
    {
        var task = MessagingTask("task-one") with
        {
            Compensation = Compensation(string.Empty, TaskKind.Messaging, TaskDispatchType.FireAndForget)
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "CompensationMessagingTopicMissing");
    }

    [Fact]
    public async Task ValidateAsync_WhenCompensationUsesCallback_ReturnsCompensationDispatchTypeNotSupported()
    {
        var task = MessagingTask("task-one") with
        {
            Compensation = Compensation("commands.sales.undo", TaskKind.Messaging, TaskDispatchType.FireAndWaitCallback)
        };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "CompensationDispatchTypeNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenCompensationRetryPolicyIsInvalid_ReturnsRetryMaxRetriesInvalid()
    {
        var compensation = Compensation("commands.sales.undo", TaskKind.Messaging, TaskDispatchType.FireAndForget) with
        {
            RetryPolicy = new RetryPolicyArtifact(
                -1,
                RetryStrategyType.Fixed,
                new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
                [],
                true)
        };
        var task = MessagingTask("task-one") with { Compensation = compensation };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "RetryMaxRetriesInvalid");
    }

    [Fact]
    public async Task ValidateAsync_WhenCompensationTimeoutPolicyIsInvalid_ReturnsTimeoutDurationInvalid()
    {
        var compensation = Compensation("commands.sales.undo", TaskKind.Messaging, TaskDispatchType.FireAndForget) with
        {
            TimeoutPolicy = new TimeoutPolicyArtifact(
                Duration.FromSeconds(0),
                TimeoutBehavior.Fail,
                new FailTimeoutBehaviorPolicyArtifact("TIMEOUT"))
        };
        var task = MessagingTask("task-one") with { Compensation = compensation };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "TimeoutDurationInvalid");
    }

    [Fact]
    public async Task ValidateAsync_WhenCompensationConditionExpressionIsMissing_ReturnsConditionExpressionMissing()
    {
        var compensation = Compensation("commands.sales.undo", TaskKind.Messaging, TaskDispatchType.FireAndForget) with
        {
            ExecutionCondition = EnabledCondition(string.Empty)
        };
        var task = MessagingTask("task-one") with { Compensation = compensation };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "ConditionExpressionMissing");
    }

    [Fact]
    public async Task ValidateAsync_WhenCompensationTransformationDslIsMissing_ReturnsTransformationDslMissing()
    {
        var compensation = Compensation("commands.sales.undo", TaskKind.Messaging, TaskDispatchType.FireAndForget) with
        {
            Transformation = EnabledTransformation(string.Empty)
        };
        var task = MessagingTask("task-one") with { Compensation = compensation };
        var artifact = CreateArtifact(stages: [Stage("stage-one", 1, tasks: [task])]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "TransformationDslMissing");
    }

    [Fact]
    public async Task ValidateAsync_WhenBranchSourceIsNotStage_ReturnsBranchSourceTypeNotSupported()
    {
        var stageOne = Stage("stage-one", 1, branchRules:
        [
            BranchRule(ElementType.Task, Id.New(), ElementType.Stage, Id.New())
        ]);
        var artifact = CreateArtifact(stages: [stageOne, Stage("stage-two", 2)]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "BranchSourceTypeNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenBranchTargetIsNotStage_ReturnsBranchTargetTypeNotSupported()
    {
        var stageOne = Stage("stage-one", 1);
        var artifact = CreateArtifact(stages:
        [
            stageOne with
            {
                BranchRules = [BranchRule(ElementType.Stage, stageOne.Id, ElementType.Task, Id.New())]
            },
            Stage("stage-two", 2)
        ]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "BranchTargetTypeNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenBranchTargetIsMissing_ReturnsBranchTargetNotFound()
    {
        var stageOne = Stage("stage-one", 1);
        var artifact = CreateArtifact(stages:
        [
            stageOne with
            {
                BranchRules = [BranchRule(ElementType.Stage, stageOne.Id, ElementType.Stage, Id.New())]
            }
        ]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "BranchTargetNotFound");
    }

    [Fact]
    public async Task ValidateAsync_WhenBranchNavigatesBackwards_ReturnsBranchTargetOrderNotSupported()
    {
        var stageOne = Stage("stage-one", 1);
        var stageTwo = Stage("stage-two", 2);
        var artifact = CreateArtifact(stages:
        [
            stageOne,
            stageTwo with
            {
                BranchRules = [BranchRule(ElementType.Stage, stageTwo.Id, ElementType.Stage, stageOne.Id)]
            }
        ]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "BranchTargetOrderNotSupported");
    }

    [Fact]
    public async Task ValidateAsync_WhenBranchConditionExpressionIsMissing_ReturnsConditionExpressionMissing()
    {
        var stageOne = Stage("stage-one", 1);
        var stageTwo = Stage("stage-two", 2);
        var artifact = CreateArtifact(stages:
        [
            stageOne with
            {
                BranchRules =
                [
                    new BranchRuleArtifact(
                        Id.New(),
                        ElementType.Stage,
                        stageOne.Id,
                        EnabledCondition(string.Empty),
                        ElementType.Stage,
                        stageTwo.Id)
                ]
            },
            stageTwo
        ]);

        var result = await ValidateAsync(artifact);

        AssertFailure(result, "ConditionExpressionMissing");
    }

    private static async Task<RuntimeArtifactCompatibilityValidationResult> ValidateAsync(OrchestrationArtifact artifact)
    {
        var payload = JsonSerializer.SerializeToNode(artifact, SerializerOptions)!;
        return await new MessagingRuntimeArtifactCompatibilityValidator().ValidateAsync(payload);
    }

    private static void AssertFailure(RuntimeArtifactCompatibilityValidationResult result, string errorCode)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    private static OrchestrationArtifact CreateArtifact(
        IReadOnlyList<StageArtifact>? stages = null,
        IReadOnlyList<TriggerBindingArtifact>? triggers = null)
        => new(
            Id.New(),
            Id.New(),
            "sales.sale.created",
            "Sale Created",
            "sales",
            Version,
            new Checksum("checksum"),
            triggers ?? [EventTrigger("events.sales.sale.created")],
            [],
            stages ?? [Stage("stage-one", 1, tasks: [MessagingTask("task-one")])]);

    private static StageArtifact Stage(
        string key,
        int order,
        ExecutionConditionArtifact? condition = null,
        IReadOnlyList<TaskArtifact>? tasks = null,
        IReadOnlyList<BranchRuleArtifact>? branchRules = null)
        => new(
            Id.New(),
            key,
            key,
            order,
            condition ?? DisabledCondition(),
            tasks ?? [],
            [],
            branchRules ?? []);

    private static TaskArtifact MessagingTask(string key)
        => new(
            Id.New(),
            key,
            key,
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            DisabledCondition(),
            DisabledTransformation(),
            MessagingConfiguration($"commands.{key}"),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static TaskArtifact HttpTask(string key)
        => MessagingTask(key) with
        {
            Kind = TaskKind.Http,
            Configuration = new HttpTaskConfigurationArtifact(
                null!,
                "Services:Demo",
                "/demo",
                "POST",
                JsonNode.Parse("{}"),
                JsonNode.Parse("{}"),
                [200],
                true),
            DispatchType = TaskDispatchType.FireAndWait
        };

    private static MessagingTaskConfigurationArtifact MessagingConfiguration(
        string topic,
        SchemaBindingArtifact? requestBinding = null,
        SchemaBindingArtifact? responseBinding = null)
        => new(topic, Version, requestBinding)
        {
            RequestSchemaBinding = requestBinding,
            ResponseSchemaBinding = responseBinding
        };

    private static CompensationArtifact Compensation(
        string topic,
        TaskKind kind,
        TaskDispatchType dispatchType)
        => new(
            kind,
            DisabledTransformation(),
            DisabledCondition(),
            MessagingConfiguration(topic),
            null,
            null,
            dispatchType);

    private static BranchRuleArtifact BranchRule(
        ElementType fromType,
        Id fromId,
        ElementType toType,
        Id toId)
        => new(Id.New(), fromType, fromId, EnabledCondition("true"), toType, toId);

    private static TriggerBindingArtifact EventTrigger(string topic)
        => new(Id.New(), TriggerType.Event, EventTriggerChannel(topic), true);

    private static EventTriggerChannelArtifact EventTriggerChannel(string topic)
        => new(SchemaBinding(validationEnabled: false), topic, Version);

    private static SchemaBindingArtifact SchemaBinding(bool validationEnabled)
        => new(
            Id.New(),
            ElementType.Task,
            Id.New(),
            Id.New(),
            "sales.sale.created",
            Version,
            Id.New(),
            false)
        {
            IsValidationEnabled = validationEnabled,
            ContractKind = SchemaContractKind.CommandRequest
        };

    private static ExecutionConditionArtifact DisabledCondition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(string.Empty)))
        {
            IsEnabled = false
        };

    private static ExecutionConditionArtifact EnabledCondition(
        string expression,
        EngineType engine = EngineType.DSL)
        => new(engine, new DslConditionConfigurationArtifact(new Expression(expression)))
        {
            IsEnabled = true
        };

    private static TransformationArtifact DisabledTransformation()
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact())
        {
            IsEnabled = false
        };

    private static TransformationArtifact EnabledTransformation(
        string dsl,
        EngineType engine = EngineType.DSL)
        => new(engine, new DslTransformationConfigurationArtifact { Dsl = dsl })
        {
            IsEnabled = true
        };

    private static ValidationArtifact DslValidation(string dsl)
        => new(EngineType.DSL, new DslValidationConfigurationArtifact { Dsl = dsl })
        {
            IsEnabled = true
        };

    private sealed class UnsupportedTimeoutBehaviorPolicyArtifact : ITimeoutBehaviorPolicyArtifact
    {
        public TimeoutBehavior Behavior => TimeoutBehavior.Fail;
    }
}
