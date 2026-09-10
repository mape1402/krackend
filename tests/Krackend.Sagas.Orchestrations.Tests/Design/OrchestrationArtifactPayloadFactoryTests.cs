using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class OrchestrationArtifactPayloadFactoryTests
{
    [Fact]
    public void CreatePayloadJson_PreservesRuntimeCriticalDesignConfiguration()
    {
        var definition = CreateDefinition();
        var version = CreateVersion(definition.Id);

        var payloadJson = new OrchestrationArtifactPayloadFactory().CreatePayloadJson(definition, version);
        var artifact = JsonNode.Parse(payloadJson)!.AsObject();
        var trigger = artifact["TriggerBindings"]!.AsArray()[0]!["TriggerChannel"]!.AsObject();
        var stage = artifact["StageDefinitions"]!.AsArray()[0]!.AsObject();
        var task = stage["TaskDefinitions"]!.AsArray()[0]!.AsObject();
        var taskConfiguration = task["Configuration"]!.AsObject();
        var compensation = task["Compensation"]!.AsObject();

        Assert.Equal("order.fulfillment", artifact["Key"]!.GetValue<string>());
        Assert.Single(artifact["TriggerBindings"]!.AsArray());
        Assert.Single(artifact["VariableDefinitions"]!.AsArray());
        Assert.Equal("orders.created", trigger["Topic"]!.GetValue<string>());
        Assert.True(trigger["Validation"]!["IsEnabled"]!.GetValue<bool>());
        Assert.Equal("trigger payload validation", trigger["Validation"]!["Configuration"]!["Dsl"]!.GetValue<string>());

        Assert.Equal("reserve-inventory", stage["Key"]!.GetValue<string>());
        Assert.True(stage["ExecutionCondition"]!["IsEnabled"]!.GetValue<bool>());
        Assert.Equal((int)EngineType.DSL, stage["ExecutionCondition"]!["Engine"]!.GetValue<int>());
        Assert.Single(stage["ParallelGroups"]!.AsArray());
        Assert.Single(stage["BranchRules"]!.AsArray());

        Assert.Equal("reserve-stock", task["Key"]!.GetValue<string>());
        Assert.Equal((int)TaskKind.Messaging, task["Kind"]!.GetValue<int>());
        Assert.Equal((int)TaskExecutionMode.Parallel, task["ExecutionMode"]!.GetValue<int>());
        Assert.Equal((int)TaskDispatchType.FireAndWaitCallback, task["DispatchType"]!.GetValue<int>());
        Assert.True(task["IsEnabled"]!.GetValue<bool>());
        Assert.Equal((int)OnErrorPolicy.StopAndCompensate, task["OnErrorPolicy"]!.GetValue<int>());
        Assert.True(task["ExecutionCondition"]!["IsEnabled"]!.GetValue<bool>());
        Assert.True(task["Transformation"]!["IsEnabled"]!.GetValue<bool>());
        Assert.Equal("inventory.reserve", taskConfiguration["Topic"]!.GetValue<string>());
        Assert.True(taskConfiguration["RequestSchemaBinding"]!["IsValidationEnabled"]!.GetValue<bool>());
        Assert.True(taskConfiguration["ResponseSchemaBinding"]!["IsValidationEnabled"]!.GetValue<bool>());
        Assert.True(taskConfiguration["RequestValidation"]!["IsEnabled"]!.GetValue<bool>());
        Assert.Equal("request payload validation", taskConfiguration["RequestValidation"]!["Configuration"]!["Dsl"]!.GetValue<string>());
        Assert.True(taskConfiguration["ResponseValidation"]!["IsEnabled"]!.GetValue<bool>());
        Assert.Equal("response payload validation", taskConfiguration["ResponseValidation"]!["Configuration"]!["Dsl"]!.GetValue<string>());
        Assert.Equal("1.0.0", taskConfiguration["Version"]!.GetValue<string>());
        Assert.Equal((int)RetryStrategyType.Fixed, task["RetryPolicy"]!["StrategyType"]!.GetValue<int>());
        Assert.Equal(3, task["RetryPolicy"]!["MaxRetries"]!.GetValue<int>());
        Assert.Equal((int)TimeoutBehavior.Reconcile, task["TimeoutPolicy"]!["TimeoutBehavior"]!.GetValue<int>());

        Assert.Equal((int)TaskKind.Messaging, compensation["CompensationTaskKind"]!.GetValue<int>());
        Assert.True(compensation["ExecutionCondition"]!["IsEnabled"]!.GetValue<bool>());
        Assert.True(compensation["Transformation"]!["IsEnabled"]!.GetValue<bool>());
        Assert.Equal("inventory.release", compensation["Configuration"]!["Topic"]!.GetValue<string>());
        Assert.True(compensation["Configuration"]!["SchemaBinding"]!["IsValidationEnabled"]!.GetValue<bool>());
        Assert.Equal((int)RetryStrategyType.Fixed, compensation["RetryPolicy"]!["StrategyType"]!.GetValue<int>());
    }

    private static OrchestrationDefinition CreateDefinition()
        => new()
        {
            Id = Id.New(),
            Key = "order.fulfillment",
            Name = "Order fulfillment",
            Domain = "orders",
            Description = "Coordinates order fulfillment.",
            CreatedBy = "tests",
            CreatedOnUtc = DateTime.UtcNow,
            IsActive = true
        };

    private static OrchestrationVersion CreateVersion(Id definitionId)
    {
        var versionId = Id.New();
        var stageId = Id.New();
        var taskId = Id.New();
        var parallelGroupId = Id.New();
        var nextStageId = Id.New();

        return new OrchestrationVersion
        {
            Id = versionId,
            OrchestrationDefinitionId = definitionId,
            Version = new SemanticVersion(1, 2, 3),
            Status = OrchestrationVersionStatus.Approved,
            VersionLabel = "1.2.3",
            Description = "Runtime parity artifact.",
            Checksum = new Checksum("checksum-123"),
            Notes = "testing",
            CreatedBy = "tests",
            CreatedOnUtc = DateTime.UtcNow,
            TriggerBindings =
            [
                new TriggerBinding
                {
                    Id = Id.New(),
                    OrchestrationVersionId = versionId,
                    Key = "order-created",
                    TriggerType = TriggerType.Event,
                    TriggerChannel = new EventTriggerChannel
                    {
                        Topic = "orders.created",
                        Version = new SemanticVersion(1, 0, 0),
                        HasSchemaValidation = true,
                        HasValidation = true,
                        Validation = DslValidation("trigger payload validation", "TriggerValidationFailed"),
                        SchemaBinding = CreateSchemaBinding(ElementType.Orchestration, definitionId, "orders.created")
                    },
                    IsEnabled = true,
                    Description = "Order created trigger."
                }
            ],
            VariableDefinitions =
            [
                new VariableDefinition
                {
                    Id = Id.New(),
                    OrchestrationVersionId = versionId,
                    Key = "inventoryTimeout",
                    DisplayName = "Inventory timeout",
                    Description = "Timeout for inventory operations.",
                    Scope = VariableScope.Environment,
                    ValueType = VariableValueType.TimeSpan,
                    DefaultValue = "00:00:30",
                    IsRequired = true,
                    IsSensitive = false
                }
            ],
            StageDefinitions =
            [
                new StageDefinition
                {
                    Id = stageId,
                    OrchestrationVersionId = versionId,
                    Key = "reserve-inventory",
                    Name = "Reserve inventory",
                    Description = "Reserve stock before payment.",
                    Order = 1,
                    ExecutionCondition = DslCondition("payload.total > 0"),
                    HasExecutionCondition = true,
                    ParallelGroups =
                    [
                        new ParallelGroupDefinition
                        {
                            Id = parallelGroupId,
                            StageDefinitionId = stageId,
                            Name = "inventory-group",
                            JoinPolicy = ParallelJoinPolicy.WaitAll,
                            MaxParallelAgents = 2
                        }
                    ],
                    BranchRules =
                    [
                        new BranchRuleDefinition
                        {
                            Id = Id.New(),
                            FromType = ElementType.Stage,
                            FromId = stageId,
                            Condition = DslCondition("outputs.reserveStock.reserved == true"),
                            NavigateToType = ElementType.Stage,
                            NavigateToId = nextStageId
                        }
                    ],
                    TaskDefinitions =
                    [
                        new TaskDefinition
                        {
                            Id = taskId,
                            StageDefinitionId = stageId,
                            Key = "reserve-stock",
                            Name = "Reserve stock",
                            Order = 1,
                            Notes = "Reserve current order stock.",
                            Kind = TaskKind.Messaging,
                            ExecutionMode = TaskExecutionMode.Parallel,
                            ParallelGroupId = parallelGroupId,
                            ExecutionCondition = DslCondition("payload.items.length > 0"),
                            HasExecutionCondition = true,
                            Transformation = DslTransformation(),
                            HasTransformation = true,
                            Configuration = new MessagingTaskConfiguration
                            {
                                Topic = "inventory.reserve",
                                Version = new SemanticVersion(1, 0, 0),
                                HasSchemaValidation = true,
                                HasRequestValidation = true,
                                RequestValidation = DslValidation("request payload validation", "RequestValidationFailed"),
                                HasResponseValidation = true,
                                ResponseValidation = DslValidation("response payload validation", "ResponseValidationFailed"),
                                SchemaBinding = CreateSchemaBinding(ElementType.Task, taskId, "inventory.reserve"),
                                RequestSchemaBinding = CreateSchemaBinding(ElementType.Task, taskId, "inventory.reserve.request"),
                                ResponseSchemaBinding = CreateSchemaBinding(ElementType.Task, taskId, "inventory.reserve.response")
                            },
                            RetryPolicy = RetryPolicy(3),
                            TimeoutPolicy = ReconcileTimeoutPolicy(),
                            OnErrorPolicy = OnErrorPolicy.StopAndCompensate,
                            CompensationDefinition = new CompensationDefinition
                            {
                                CompensationTaskKind = TaskKind.Messaging,
                                Transformation = DslTransformation(),
                                HasTransformation = true,
                                ExecutionCondition = DslCondition("outputs.reserveStock.reserved == true"),
                                HasExecutionCondition = true,
                                Configuration = new MessagingTaskConfiguration
                                {
                                    Topic = "inventory.release",
                                    Version = new SemanticVersion(1, 0, 0),
                                    HasSchemaValidation = true,
                                    SchemaBinding = CreateSchemaBinding(ElementType.Task, taskId, "inventory.release")
                                },
                                RetryPolicy = RetryPolicy(2),
                                TimeoutPolicy = FailTimeoutPolicy(),
                                DispatchType = TaskDispatchType.FireAndWaitCallback
                            },
                            DispatchType = TaskDispatchType.FireAndWaitCallback,
                            IsEnabled = true
                        }
                    ]
                }
            ]
        };
    }

    private static ExecutionCondition DslCondition(string expression)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression(expression) }
        };

    private static TransformationDefinition DslTransformation()
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration()
        };

    private static ValidationDefinition DslValidation(string dsl, string errorCode)
        => new()
        {
            Engine = EngineType.DSL,
            ErrorCode = errorCode,
            Configuration = new DslValidationConfiguration
            {
                Dsl = dsl,
                SchemaHash = "schema-hash",
                SemanticDiagnosticsJson = "{}"
            }
        };

    private static RetryPolicy RetryPolicy(int maxRetries)
        => new()
        {
            MaxRetries = maxRetries,
            StrategyType = RetryStrategyType.Fixed,
            Strategy = new FixedRetryStrategy { Delay = Duration.FromSeconds(5) },
            RetryableErrorCodes = ["TemporaryFailure", "Timeout"],
            StopOnNonRetryableError = true
        };

    private static TimeoutPolicy ReconcileTimeoutPolicy()
        => new()
        {
            Timeout = Duration.FromSeconds(30),
            TimeoutBehavior = TimeoutBehavior.Reconcile,
            TimeoutBehaviorPolicy = new ReconcileTimeoutBehaviorPolicy
            {
                OrchestrationAction = OrchestrationActionOnTimeout.Block,
                RetryPolicy = RetryPolicy(2)
            }
        };

    private static TimeoutPolicy FailTimeoutPolicy()
        => new()
        {
            Timeout = Duration.FromSeconds(10),
            TimeoutBehavior = TimeoutBehavior.Fail,
            TimeoutBehaviorPolicy = new FailTimeoutBehaviorPolicy { ErrorCode = "CompensationTimeout" }
        };

    private static SchemaBinding CreateSchemaBinding(ElementType elementType, Id elementId, string contractKey)
        => new()
        {
            Id = Id.New(),
            ElementType = elementType,
            ElementId = elementId,
            ContractId = Id.New(),
            ContractKey = contractKey,
            ContractVersion = new SemanticVersion(1, 0, 0),
            RegistryProviderId = Id.New(),
            StrictMode = true,
            IsValidationEnabled = true
        };
}
