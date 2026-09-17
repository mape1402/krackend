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
using Krackend.Sagas.Orchestrations.SchemaRegistry;

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
        Assert.Equal("payload.total > 0", stage["ExecutionCondition"]!["Configuration"]!["Expression"]!["Value"]!.GetValue<string>());
        Assert.Single(stage["ParallelGroups"]!.AsArray());
        Assert.Single(stage["BranchRules"]!.AsArray());
        Assert.Equal("outputs.reserveStock.reserved == true", stage["BranchRules"]!.AsArray()[0]!["Condition"]!["Configuration"]!["Expression"]!["Value"]!.GetValue<string>());

        Assert.Equal("reserve-stock", task["Key"]!.GetValue<string>());
        Assert.Equal((int)TaskKind.Messaging, task["Kind"]!.GetValue<int>());
        Assert.Equal((int)TaskExecutionMode.Parallel, task["ExecutionMode"]!.GetValue<int>());
        Assert.Equal((int)TaskDispatchType.FireAndWaitCallback, task["DispatchType"]!.GetValue<int>());
        Assert.True(task["IsEnabled"]!.GetValue<bool>());
        Assert.Equal((int)OnErrorPolicy.StopAndCompensate, task["OnErrorPolicy"]!.GetValue<int>());
        Assert.True(task["ExecutionCondition"]!["IsEnabled"]!.GetValue<bool>());
        Assert.Equal("payload.items.length > 0", task["ExecutionCondition"]!["Configuration"]!["Expression"]!["Value"]!.GetValue<string>());
        Assert.True(task["Transformation"]!["IsEnabled"]!.GetValue<bool>());
        Assert.Equal("inventory.reserve", taskConfiguration["Topic"]!.GetValue<string>());
        Assert.True(taskConfiguration["RequestSchemaBinding"]!["IsValidationEnabled"]!.GetValue<bool>());
        Assert.True(taskConfiguration["ResponseSchemaBinding"]!["IsValidationEnabled"]!.GetValue<bool>());
        Assert.Equal("knowl:inventory.reserve.request", taskConfiguration["RequestSchemaBinding"]!["Snapshot"]!["ContractId"]!.GetValue<string>());
        Assert.Equal("source:inventory.reserve.request", taskConfiguration["RequestSchemaBinding"]!["Snapshot"]!["SourceArtifactId"]!.GetValue<string>());
        Assert.Equal("knowl", taskConfiguration["RequestSchemaBinding"]!["Snapshot"]!["ResolvedBy"]!.GetValue<string>());
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
        Assert.Equal("outputs.reserveStock.reserved == true", compensation["ExecutionCondition"]!["Configuration"]!["Expression"]!["Value"]!.GetValue<string>());
        Assert.True(compensation["Transformation"]!["IsEnabled"]!.GetValue<bool>());
        Assert.Equal("inventory.release", compensation["Configuration"]!["Topic"]!.GetValue<string>());
        Assert.True(compensation["Configuration"]!["SchemaBinding"]!["IsValidationEnabled"]!.GetValue<bool>());
        Assert.Equal((int)RetryStrategyType.Fixed, compensation["RetryPolicy"]!["StrategyType"]!.GetValue<int>());
    }

    [Fact]
    public void CreatePayloadJson_WhenTaskHasNoOptionalPolicies_EmitsDeployableArtifact()
    {
        var definition = CreateDefinition();
        var version = CreateVersionWithoutOptionalTaskPolicies(definition.Id);

        var payloadJson = new OrchestrationArtifactPayloadFactory().CreatePayloadJson(definition, version);
        var artifact = JsonNode.Parse(payloadJson)!.AsObject();
        var task = artifact["StageDefinitions"]!.AsArray()[0]!["TaskDefinitions"]!.AsArray()[0]!.AsObject();

        Assert.Equal((int)TaskKind.Messaging, task["Kind"]!.GetValue<int>());
        Assert.Equal((int)TaskDispatchType.FireAndWaitCallback, task["DispatchType"]!.GetValue<int>());
        Assert.Null(task["RetryPolicy"]);
        Assert.Null(task["TimeoutPolicy"]);
        Assert.Null(task["Compensation"]);
    }

    [Fact]
    public void CreatePayloadJson_WhenMessagingUsesCommandBinding_NormalizesRuntimeRequestAndResponseBindings()
    {
        var definition = CreateDefinition();
        var version = CreateVersionWithoutOptionalTaskPolicies(definition.Id);
        var task = version.StageDefinitions[0].TaskDefinitions[0];
        task.Configuration = new MessagingTaskConfiguration
        {
            Topic = "inventory.reserve",
            Version = new SemanticVersion(1, 0, 0),
            SchemaBinding = CreateSchemaBinding(
                ElementType.Task,
                task.Id,
                "inventory.reserve",
                SchemaContractKind.Command),
            HasSchemaValidation = true
        };

        var payloadJson = new OrchestrationArtifactPayloadFactory().CreatePayloadJson(definition, version);
        var taskConfiguration = JsonNode.Parse(payloadJson)!["StageDefinitions"]!.AsArray()[0]!["TaskDefinitions"]!.AsArray()[0]!["Configuration"]!.AsObject();

        Assert.Equal((int)SchemaContractKind.CommandRequest, taskConfiguration["SchemaBinding"]!["ContractKind"]!.GetValue<int>());
        Assert.Equal((int)SchemaContractKind.CommandRequest, taskConfiguration["RequestSchemaBinding"]!["ContractKind"]!.GetValue<int>());
        Assert.Equal((int)SchemaContractKind.CommandResponse, taskConfiguration["ResponseSchemaBinding"]!["ContractKind"]!.GetValue<int>());
        Assert.Equal("inventory.reserve", taskConfiguration["RequestSchemaBinding"]!["ContractKey"]!.GetValue<string>());
        Assert.Equal("inventory.reserve", taskConfiguration["ResponseSchemaBinding"]!["ContractKey"]!.GetValue<string>());
    }

    [Fact]
    public void CreatePayloadJson_MapsHttpPluginHumanAndWaitTimeoutConfigurations()
    {
        var definition = CreateDefinition();
        var version = CreateVersionWithMixedTaskConfigurations(definition.Id);

        var payloadJson = new OrchestrationArtifactPayloadFactory().CreatePayloadJson(definition, version);
        var tasks = JsonNode.Parse(payloadJson)!["StageDefinitions"]!.AsArray()[0]!["TaskDefinitions"]!.AsArray();
        var httpTask = tasks[0]!.AsObject();
        var pluginTask = tasks[1]!.AsObject();
        var humanTask = tasks[2]!.AsObject();

        Assert.Equal("http", httpTask["Configuration"]!["$artifactType"]!.GetValue<string>());
        Assert.Equal("customer-api", httpTask["Configuration"]!["BaseUrlVariableRef"]!.GetValue<string>());
        Assert.Equal("/customers/{customerId}", httpTask["Configuration"]!["RelativePath"]!.GetValue<string>());
        Assert.Equal("POST", httpTask["Configuration"]!["Method"]!.GetValue<string>());
        Assert.Equal("Bearer ${token}", httpTask["Configuration"]!["HeadersTemplate"]!["Authorization"]!.GetValue<string>());
        Assert.Equal("full", httpTask["Configuration"]!["QueryTemplate"]!["mode"]!.GetValue<string>());
        Assert.Equal(202, httpTask["Configuration"]!["ExpectedStatusCodes"]!.AsArray()[1]!.GetValue<int>());
        Assert.True(httpTask["Configuration"]!["AllowSyncResponse"]!.GetValue<bool>());
        Assert.Equal("wait", httpTask["TimeoutPolicy"]!["TimeoutBehaviorPolicy"]!["$artifactType"]!.GetValue<string>());
        Assert.Equal((int)OrchestrationActionOnTimeout.Continue, httpTask["TimeoutPolicy"]!["TimeoutBehaviorPolicy"]!["OrchestrationAction"]!.GetValue<int>());

        Assert.Equal("plugin", pluginTask["Configuration"]!["$artifactType"]!.GetValue<string>());
        Assert.NotNull(pluginTask["Configuration"]!["PluginId"]);
        Assert.Equal("humanApproval", humanTask["Configuration"]!["$artifactType"]!.GetValue<string>());
    }

    [Fact]
    public void CreatePayloadJson_UsesFallbackValidationArtifactWhenConfigurationIsMissing()
    {
        var definition = CreateDefinition();
        var version = CreateVersionWithoutOptionalTaskPolicies(definition.Id);
        version.TriggerBindings.Add(new TriggerBinding
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "sale-created",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new EventTriggerChannel
            {
                Topic = "sales.sale.created",
                Version = new SemanticVersion(1, 0, 0),
                HasValidation = true,
                Validation = new ValidationDefinition
                {
                    Engine = EngineType.DSL,
                    ErrorCode = string.Empty,
                    Configuration = null!
                }
            }
        });

        var payloadJson = new OrchestrationArtifactPayloadFactory().CreatePayloadJson(definition, version);
        var validation = JsonNode.Parse(payloadJson)!["TriggerBindings"]!.AsArray()[0]!["TriggerChannel"]!["Validation"]!.AsObject();

        Assert.True(validation["IsEnabled"]!.GetValue<bool>());
        Assert.Equal("TriggerValidationFailed", validation["ErrorCode"]!.GetValue<string>());
        Assert.Equal("dsl", validation["Configuration"]!["$artifactType"]!.GetValue<string>());
    }

    [Fact]
    public void CreatePayloadJson_ThrowsForUnsupportedDesignConfigurations()
    {
        var definition = CreateDefinition();
        var unsupportedCases = new Dictionary<string, Action<OrchestrationVersion>>(StringComparer.Ordinal)
        {
            ["Trigger channel"] = version => version.TriggerBindings.Add(new TriggerBinding
            {
                Id = Id.New(),
                OrchestrationVersionId = version.Id,
                Key = "unsupported",
                TriggerType = TriggerType.Event,
                TriggerChannel = new UnsupportedTriggerChannel()
            }),
            ["Condition configuration"] = version =>
            {
                version.StageDefinitions[0].HasExecutionCondition = true;
                version.StageDefinitions[0].ExecutionCondition = new ExecutionCondition
                {
                    Engine = EngineType.DSL,
                    Configuration = new UnsupportedConditionConfiguration()
                };
            },
            ["Transformation configuration"] = version =>
            {
                var task = version.StageDefinitions[0].TaskDefinitions[0];
                task.HasTransformation = true;
                task.Transformation = new TransformationDefinition
                {
                    Engine = EngineType.DSL,
                    Configuration = new UnsupportedTransformationConfiguration()
                };
            },
            ["Validation configuration"] = version =>
            {
                var messaging = (MessagingTaskConfiguration)version.StageDefinitions[0].TaskDefinitions[0].Configuration;
                messaging.HasRequestValidation = true;
                messaging.RequestValidation = new ValidationDefinition
                {
                    Engine = EngineType.DSL,
                    Configuration = new UnsupportedValidationConfiguration()
                };
            },
            ["Task configuration"] = version => version.StageDefinitions[0].TaskDefinitions[0].Configuration = new UnsupportedTaskConfiguration(),
            ["Retry strategy"] = version => version.StageDefinitions[0].TaskDefinitions[0].RetryPolicy = new RetryPolicy
            {
                MaxRetries = 1,
                StrategyType = RetryStrategyType.Fixed,
                Strategy = new UnsupportedRetryStrategy()
            },
            ["Timeout behavior policy"] = version => version.StageDefinitions[0].TaskDefinitions[0].TimeoutPolicy = new TimeoutPolicy
            {
                Timeout = Duration.FromSeconds(1),
                TimeoutBehavior = TimeoutBehavior.Fail,
                TimeoutBehaviorPolicy = new UnsupportedTimeoutBehaviorPolicy()
            }
        };

        foreach (var unsupportedCase in unsupportedCases)
        {
            var version = CreateVersionWithoutOptionalTaskPolicies(definition.Id);
            unsupportedCase.Value(version);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                new OrchestrationArtifactPayloadFactory().CreatePayloadJson(definition, version));

            Assert.Contains(unsupportedCase.Key.Split(' ')[0], exception.Message, StringComparison.OrdinalIgnoreCase);
        }
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

    private static OrchestrationVersion CreateVersionWithoutOptionalTaskPolicies(Id definitionId)
    {
        var versionId = Id.New();
        var stageId = Id.New();
        var taskId = Id.New();

        return new OrchestrationVersion
        {
            Id = versionId,
            OrchestrationDefinitionId = definitionId,
            Version = new SemanticVersion(1, 0, 0),
            Status = OrchestrationVersionStatus.Deployed,
            VersionLabel = "1.0.0",
            Description = "Runtime parity artifact.",
            Checksum = new Checksum("checksum-without-optional-policies"),
            Notes = "testing",
            CreatedBy = "tests",
            CreatedOnUtc = DateTime.UtcNow,
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
                    HasExecutionCondition = false,
                    TaskDefinitions =
                    [
                        new TaskDefinition
                        {
                            Id = taskId,
                            StageDefinitionId = stageId,
                            Key = "reserve-stock",
                            Name = "Reserve stock",
                            Order = 1,
                            Notes = string.Empty,
                            Kind = TaskKind.Messaging,
                            ExecutionMode = TaskExecutionMode.Sequential,
                            HasExecutionCondition = false,
                            HasTransformation = false,
                            Configuration = new MessagingTaskConfiguration
                            {
                                Topic = "inventory.reserve",
                                Version = new SemanticVersion(1, 0, 0)
                            },
                            OnErrorPolicy = OnErrorPolicy.Stop,
                            DispatchType = TaskDispatchType.FireAndWaitCallback,
                            IsEnabled = true
                        }
                    ]
                }
            ]
        };
    }

    private static OrchestrationVersion CreateVersionWithMixedTaskConfigurations(Id definitionId)
    {
        var versionId = Id.New();
        var stageId = Id.New();

        return new OrchestrationVersion
        {
            Id = versionId,
            OrchestrationDefinitionId = definitionId,
            Version = new SemanticVersion(2, 0, 0),
            Status = OrchestrationVersionStatus.Approved,
            VersionLabel = "2.0.0",
            Checksum = new Checksum("checksum-mixed-task-configurations"),
            CreatedBy = "tests",
            CreatedOnUtc = DateTime.UtcNow,
            StageDefinitions =
            [
                new StageDefinition
                {
                    Id = stageId,
                    OrchestrationVersionId = versionId,
                    Key = "mixed",
                    Name = "Mixed stage",
                    Order = 1,
                    TaskDefinitions =
                    [
                        new TaskDefinition
                        {
                            Id = Id.New(),
                            StageDefinitionId = stageId,
                            Key = "http.customer",
                            Name = "HTTP customer",
                            Order = 1,
                            Kind = TaskKind.Http,
                            Configuration = new HttpTaskConfiguration
                            {
                                BaseUrlVariableRef = "customer-api",
                                RelativePath = "/customers/{customerId}",
                                Method = "POST",
                                HeadersTemplate = JsonNode.Parse("""{"Authorization":"Bearer ${token}"}"""),
                                QueryTemplate = JsonNode.Parse("""{"mode":"full"}"""),
                                ExpectedStatusCodes = [200, 202],
                                AllowSyncResponse = true,
                                HasSchemaValidation = true,
                                SchemaBinding = CreateSchemaBinding(ElementType.Task, Id.New(), "customer.command")
                            },
                            TimeoutPolicy = new TimeoutPolicy
                            {
                                Timeout = Duration.FromSeconds(10),
                                TimeoutBehavior = TimeoutBehavior.Wait,
                                TimeoutBehaviorPolicy = new WaitTimeoutBehaviorPolicy
                                {
                                    OrchestrationAction = OrchestrationActionOnTimeout.Continue,
                                    WaitingTime = Duration.FromSeconds(3)
                                }
                            },
                            OnErrorPolicy = OnErrorPolicy.Stop,
                            DispatchType = TaskDispatchType.FireAndForget,
                            IsEnabled = true
                        },
                        new TaskDefinition
                        {
                            Id = Id.New(),
                            StageDefinitionId = stageId,
                            Key = "plugin.credit",
                            Name = "Plugin credit",
                            Order = 2,
                            Kind = TaskKind.Plugin,
                            Configuration = new PluginTaskConfiguration { PluginId = Id.New() },
                            OnErrorPolicy = OnErrorPolicy.Stop,
                            DispatchType = TaskDispatchType.FireAndForget,
                            IsEnabled = true
                        },
                        new TaskDefinition
                        {
                            Id = Id.New(),
                            StageDefinitionId = stageId,
                            Key = "approval.manual",
                            Name = "Manual approval",
                            Order = 3,
                            Kind = TaskKind.HumanApproval,
                            Configuration = new HumanApprovalTaskConfiguration(),
                            OnErrorPolicy = OnErrorPolicy.Stop,
                            DispatchType = TaskDispatchType.FireAndForget,
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

    private static SchemaBinding CreateSchemaBinding(
        ElementType elementType,
        Id elementId,
        string contractKey,
        SchemaContractKind contractKind = SchemaContractKind.CommandRequest)
        => new()
        {
            Id = Id.New(),
            ElementType = elementType,
            ElementId = elementId,
            ContractId = Id.New(),
            ContractKey = contractKey,
            ContractVersion = new SemanticVersion(1, 0, 0),
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "knowl",
            StrictMode = true,
            IsValidationEnabled = true,
            ContractKind = contractKind,
            Snapshot = new Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot
            {
                ContractKind = contractKind,
                RegistryProviderId = "provider-001",
                RegistryProviderKey = "knowl",
                ContractId = $"knowl:{contractKey}",
                ContractKey = contractKey,
                ContractVersion = "1.0.0",
                SchemaFormat = "ButterMorph",
                SchemaJson = """{"type":"object"}""",
                ContentHash = $"hash:{contractKey}",
                SourceArtifactId = $"source:{contractKey}",
                ResolvedBy = "knowl",
                ResolvedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
            }
        };

    private sealed class UnsupportedTriggerChannel : ITriggerChannel
    {
        public TriggerType TriggerType => TriggerType.Event;

        public SchemaBinding SchemaBinding { get; set; } = null!;
    }

    private sealed class UnsupportedConditionConfiguration : IConditionConfiguration
    {
        public EngineType Engine => EngineType.DSL;
    }

    private sealed class UnsupportedTransformationConfiguration : ITransformationConfiguration
    {
        public EngineType Engine => EngineType.DSL;
    }

    private sealed class UnsupportedValidationConfiguration : IValidationConfiguration
    {
        public EngineType Engine => EngineType.DSL;
    }

    private sealed class UnsupportedTaskConfiguration : ITaskConfiguration
    {
        public TaskKind Kind => TaskKind.Messaging;
    }

    private sealed class UnsupportedRetryStrategy : IRetryStrategy
    {
        public RetryStrategyType Type => RetryStrategyType.Fixed;
    }

    private sealed class UnsupportedTimeoutBehaviorPolicy : ITimeoutBehaviorPolicy
    {
        public TimeoutBehavior Behavior => TimeoutBehavior.Fail;
    }
}
