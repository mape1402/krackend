using System.Reflection;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using DesignSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;
using TaskDefinitionModel = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TaskDefinition;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class DefinitionEntityMapperTests
{
    [Fact]
    public void TaskMapperRoundTripsHttpPluginHumanApprovalAndCompensationConfigurations()
    {
        var stageId = Id.New();
        var httpTask = CreateTask(
            stageId,
            TaskKind.Http,
            new HttpTaskConfiguration
            {
                BaseUrlVariableRef = "inventoryApi",
                RelativePath = "/api/v1/inventory",
                Method = "POST",
                HeadersTemplate = JsonNode.Parse("""{"x-tenant":"demo"}"""),
                QueryTemplate = JsonNode.Parse("""{"verbose":"true"}"""),
                ExpectedStatusCodes = [200, 202],
                AllowSyncResponse = true,
                SchemaBinding = CreateSchemaBinding(SchemaContractKind.CommandRequest),
                HasSchemaValidation = true
            });
        httpTask.TimeoutPolicy = new TimeoutPolicy
        {
            Timeout = new Duration(TimeSpan.FromSeconds(15)),
            TimeoutBehavior = TimeoutBehavior.Wait,
            TimeoutBehaviorPolicy = new WaitTimeoutBehaviorPolicy
            {
                OrchestrationAction = OrchestrationActionOnTimeout.Continue,
                WaitingTime = new Duration(TimeSpan.FromSeconds(30))
            }
        };

        var httpRoundTrip = ToDefinition(ToEntity(httpTask));
        var httpConfig = Assert.IsType<HttpTaskConfiguration>(httpRoundTrip.Configuration);
        Assert.Equal("inventoryApi", httpConfig.BaseUrlVariableRef);
        Assert.Equal([200, 202], httpConfig.ExpectedStatusCodes);
        Assert.True(httpConfig.AllowSyncResponse);
        Assert.IsType<WaitTimeoutBehaviorPolicy>(httpRoundTrip.TimeoutPolicy!.TimeoutBehaviorPolicy);

        var pluginId = Id.New();
        var pluginRoundTrip = ToDefinition(ToEntity(CreateTask(
            stageId,
            TaskKind.Plugin,
            new PluginTaskConfiguration { PluginId = pluginId })));
        Assert.Equal(pluginId, Assert.IsType<PluginTaskConfiguration>(pluginRoundTrip.Configuration).PluginId);

        var approvalRoundTrip = ToDefinition(ToEntity(CreateTask(
            stageId,
            TaskKind.HumanApproval,
            new HumanApprovalTaskConfiguration())));
        Assert.IsType<HumanApprovalTaskConfiguration>(approvalRoundTrip.Configuration);

        var compensation = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Plugin,
            Configuration = new PluginTaskConfiguration { PluginId = pluginId },
            DispatchType = TaskDispatchType.FireAndForget
        };
        var compensatedTask = CreateTask(
            stageId,
            TaskKind.Messaging,
            CreateMessagingConfiguration(stageId));
        compensatedTask.CompensationDefinition = compensation;

        var compensatedRoundTrip = ToDefinition(ToEntity(compensatedTask));

        Assert.NotNull(compensatedRoundTrip.CompensationDefinition);
        Assert.IsType<PluginTaskConfiguration>(compensatedRoundTrip.CompensationDefinition!.Configuration);
        Assert.Null(compensatedRoundTrip.CompensationDefinition.RetryPolicy);
        Assert.Null(compensatedRoundTrip.CompensationDefinition.TimeoutPolicy);
    }

    [Fact]
    public void TaskMapperRoundTripsMessagingRequestResponseValidationAndSnapshots()
    {
        var stageId = Id.New();
        var task = CreateTask(
            stageId,
            TaskKind.Messaging,
            CreateMessagingConfiguration(stageId));
        task.ExecutionCondition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("payload.total > 0") }
        };
        task.HasExecutionCondition = true;
        task.Transformation = new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration
            {
                Dsl = "map response",
                SourceContextHash = "source-hash",
                TargetSchemaHash = "target-hash",
                SemanticDiagnosticsJson = string.Empty
            }
        };
        task.HasTransformation = true;
        task.TimeoutPolicy = new TimeoutPolicy
        {
            Timeout = new Duration(TimeSpan.FromMinutes(1)),
            TimeoutBehavior = TimeoutBehavior.Reconcile,
            TimeoutBehaviorPolicy = new ReconcileTimeoutBehaviorPolicy
            {
                OrchestrationAction = OrchestrationActionOnTimeout.Block,
                RetryPolicy = CreateRetryPolicy()
            }
        };

        var roundTrip = ToDefinition(ToEntity(task));
        var messaging = Assert.IsType<MessagingTaskConfiguration>(roundTrip.Configuration);

        Assert.True(roundTrip.HasExecutionCondition);
        Assert.True(roundTrip.HasTransformation);
        Assert.Equal("source-hash", Assert.IsType<DslTransformationConfiguration>(roundTrip.Transformation!.Configuration).SourceContextHash);
        Assert.True(messaging.HasRequestValidation);
        Assert.True(messaging.HasResponseValidation);
        Assert.Equal(SchemaContractKind.CommandRequest, messaging.RequestSchemaBinding!.ContractKind);
        Assert.Equal(SchemaContractKind.CommandResponse, messaging.ResponseSchemaBinding!.ContractKind);
        Assert.Equal("request-hash", messaging.RequestSchemaBinding.Snapshot!.ContentHash);
        Assert.Equal("ResponseRejected", messaging.ResponseValidation!.ErrorCode);
        Assert.IsType<ReconcileTimeoutBehaviorPolicy>(roundTrip.TimeoutPolicy!.TimeoutBehaviorPolicy);
    }

    [Fact]
    public void EntityMapperReadsDefaultAndLegacyJsonBranches()
    {
        var stageEntity = new StageDefinitionEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = Id.New(),
            Key = "empty-condition",
            Name = "Empty condition",
            Order = 1,
            ExecutionCondition = new ExecutionConditionJsonModel
            {
                IsEnabled = true,
                Engine = EngineType.DSL,
                Configuration = new ConditionConfigurationEnvelopeJsonModel()
            }
        };
        var stage = ToDefinition(stageEntity);
        Assert.Null(stage.ExecutionCondition);
        Assert.True(stage.HasExecutionCondition);

        var taskEntity = new TaskDefinitionEntity
        {
            Id = Id.New(),
            StageDefinitionId = stageEntity.Id,
            Key = "legacy-task",
            Name = "Legacy task",
            Kind = TaskKind.Messaging,
            Configuration = new TaskConfigurationEnvelopeJsonModel
            {
                Type = "messaging",
                Messaging = new MessagingTaskConfigurationJsonModel
                {
                    Topic = "legacy.topic",
                    Version = "1.2.3"
                }
            },
            Transformation = new TransformationDefinitionJsonModel
            {
                IsEnabled = true,
                Engine = EngineType.DSL,
                Configuration = new TransformationConfigurationEnvelopeJsonModel()
            },
            RetryPolicy = new RetryPolicyJsonModel
            {
                StrategyType = RetryStrategyType.Fixed,
                Strategy = new RetryStrategyEnvelopeJsonModel { Type = "unknown" }
            },
            TimeoutPolicy = new TimeoutPolicyJsonModel
            {
                Timeout = TimeSpan.FromSeconds(5),
                TimeoutBehavior = TimeoutBehavior.Fail,
                TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel { Type = "unknown" }
            }
        };

        var task = ToDefinition(taskEntity);
        var messaging = Assert.IsType<MessagingTaskConfiguration>(task.Configuration);
        Assert.False(messaging.HasSchemaValidation);
        Assert.IsType<FixedRetryStrategy>(task.RetryPolicy!.Strategy);
        Assert.IsType<FailTimeoutBehaviorPolicy>(task.TimeoutPolicy!.TimeoutBehaviorPolicy);
        Assert.Equal("{}", Assert.IsType<DslTransformationConfiguration>(task.Transformation!.Configuration).SemanticDiagnosticsJson);

        var trigger = ToDefinition(new TriggerBindingEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = Id.New(),
            Key = "legacy-trigger",
            TriggerType = TriggerType.Event,
            TriggerChannel = new TriggerChannelEnvelopeJsonModel { Type = "unknown" }
        });
        Assert.IsType<EventTriggerChannel>(trigger.TriggerChannel);
    }

    [Fact]
    public void EntityMapperRejectsUnsupportedPolymorphicConfiguration()
    {
        var task = CreateTask(
            Id.New(),
            TaskKind.Messaging,
            CreateMessagingConfiguration(Id.New()));
        task.RetryPolicy = new RetryPolicy
        {
            StrategyType = RetryStrategyType.Fixed,
            Strategy = new UnsupportedRetryStrategy()
        };

        var exception = Assert.Throws<NotSupportedException>(() => ToEntity(task));

        Assert.Contains(nameof(UnsupportedRetryStrategy), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("condition")]
    [InlineData("transformation")]
    [InlineData("timeout")]
    [InlineData("task")]
    [InlineData("trigger")]
    [InlineData("validation")]
    public void EntityMapperRejectsUnsupportedNestedConfigurationTypes(string unsupportedPart)
    {
        var stageId = Id.New();
        var versionId = Id.New();

        var exception = Assert.Throws<NotSupportedException>(() =>
        {
            switch (unsupportedPart)
            {
                case "condition":
                    ToEntity(new StageDefinition
                    {
                        Id = stageId,
                        OrchestrationVersionId = versionId,
                        Key = "stage",
                        Name = "Stage",
                        Order = 1,
                        HasExecutionCondition = true,
                        ExecutionCondition = new ExecutionCondition
                        {
                            Engine = EngineType.DSL,
                            Configuration = new UnsupportedConditionConfiguration()
                        }
                    });
                    break;
                case "transformation":
                    var transformed = CreateTask(stageId, TaskKind.Messaging, CreateMessagingConfiguration(stageId));
                    transformed.HasTransformation = true;
                    transformed.Transformation = new TransformationDefinition
                    {
                        Engine = EngineType.DSL,
                        Configuration = new UnsupportedTransformationConfiguration()
                    };
                    ToEntity(transformed);
                    break;
                case "timeout":
                    var timed = CreateTask(stageId, TaskKind.Messaging, CreateMessagingConfiguration(stageId));
                    timed.TimeoutPolicy = new TimeoutPolicy
                    {
                        Timeout = new Duration(TimeSpan.FromSeconds(1)),
                        TimeoutBehavior = TimeoutBehavior.Fail,
                        TimeoutBehaviorPolicy = new UnsupportedTimeoutBehaviorPolicy()
                    };
                    ToEntity(timed);
                    break;
                case "task":
                    ToEntity(CreateTask(stageId, TaskKind.Plugin, new UnsupportedTaskConfiguration()));
                    break;
                case "trigger":
                    ToEntity(new TriggerBinding
                    {
                        Id = Id.New(),
                        OrchestrationVersionId = versionId,
                        Key = "trigger",
                        TriggerType = TriggerType.Event,
                        TriggerChannel = new UnsupportedTriggerChannel()
                    });
                    break;
                case "validation":
                    var validated = CreateTask(stageId, TaskKind.Messaging, CreateMessagingConfiguration(stageId));
                    ((MessagingTaskConfiguration)validated.Configuration).RequestValidation = new ValidationDefinition
                    {
                        Engine = EngineType.DSL,
                        Configuration = new UnsupportedValidationConfiguration()
                    };
                    ToEntity(validated);
                    break;
            }
        });

        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EntityMapperReadsNullOptionalConfigurationBranches()
    {
        var stage = ToDefinition(new StageDefinitionEntity
        {
            Id = Id.New(),
            OrchestrationVersionId = Id.New(),
            Key = "stage",
            Name = "Stage",
            Order = 1,
            ExecutionCondition = new ExecutionConditionJsonModel
            {
                IsEnabled = true,
                Engine = EngineType.DSL,
                Configuration = null
            }
        });
        var task = ToDefinition(new TaskDefinitionEntity
        {
            Id = Id.New(),
            StageDefinitionId = Id.New(),
            Key = "task",
            Name = "Task",
            Kind = TaskKind.HumanApproval,
            Configuration = null!
        });

        Assert.Null(stage.ExecutionCondition);
        Assert.IsType<HumanApprovalTaskConfiguration>(task.Configuration);
    }

    [Fact]
    public void EntityMapperOptionalHelpersHandleNullAndFallbackBranches()
    {
        var condition = InvokePrivate<ExecutionConditionJsonModel?>(
            "ToOptionalJson",
            [typeof(ExecutionCondition)],
            [null]);
        var transformation = InvokePrivate<TransformationDefinitionJsonModel?>(
            "ToOptionalJson",
            [typeof(TransformationDefinition)],
            [null]);
        var validationEnvelope = InvokePrivate<ValidationConfigurationEnvelopeJsonModel>(
            "ToJson",
            [typeof(IValidationConfiguration)],
            [null]);
        var validation = InvokePrivate<ValidationDefinition>(
            "ToModel",
            [typeof(ValidationDefinitionJsonModel)],
            [
                new ValidationDefinitionJsonModel
                {
                    IsEnabled = true,
                    Configuration = new ValidationConfigurationEnvelopeJsonModel
                    {
                        Type = "legacy",
                        Dsl = new DslValidationConfigurationJsonModel
                        {
                            Dsl = "validate legacy",
                            SchemaHash = "schema",
                            SemanticDiagnosticsJson = string.Empty
                        }
                    }
                }
            ]);
        var snapshot = InvokePrivate<SchemaContractSnapshotJsonModel?>(
            "ToJson",
            [typeof(DesignSchemaContractSnapshot)],
            [null]);

        Assert.Null(condition);
        Assert.Null(transformation);
        Assert.Equal("dsl", validationEnvelope.Type);
        Assert.NotNull(validationEnvelope.Dsl);
        Assert.Equal("PayloadValidationFailed", validation.ErrorCode);
        Assert.Equal("{}", Assert.IsType<DslValidationConfiguration>(validation.Configuration).SemanticDiagnosticsJson);
        Assert.Null(snapshot);
    }

    [Fact]
    public void VersionMapperRejectsInvalidSemanticVersion()
    {
        var entity = new OrchestrationVersionEntity
        {
            Id = Id.New(),
            OrchestrationDefinitionId = Id.New(),
            Version = "bad",
            Checksum = "checksum"
        };

        var exception = Assert.Throws<FormatException>(() => ToDefinition(entity));

        Assert.Contains("Invalid semantic version", exception.Message, StringComparison.Ordinal);
    }

    private static TaskDefinitionEntity ToEntity(TaskDefinitionModel task)
        => Invoke<TaskDefinitionEntity>("ToEntity", task);

    private static TaskDefinitionModel ToDefinition(TaskDefinitionEntity entity)
        => Invoke<TaskDefinitionModel>("ToDefinition", entity);

    private static StageDefinition ToDefinition(StageDefinitionEntity entity)
        => Invoke<StageDefinition>("ToDefinition", entity);

    private static TriggerBinding ToDefinition(TriggerBindingEntity entity)
        => Invoke<TriggerBinding>("ToDefinition", entity);

    private static StageDefinitionEntity ToEntity(StageDefinition stage)
        => Invoke<StageDefinitionEntity>("ToEntity", stage);

    private static TriggerBindingEntity ToEntity(TriggerBinding trigger)
        => Invoke<TriggerBindingEntity>("ToEntity", trigger);

    private static OrchestrationVersion ToDefinition(OrchestrationVersionEntity entity)
        => Invoke<OrchestrationVersion>("ToDefinition", entity);

    private static TResult Invoke<TResult>(string methodName, object parameter)
    {
        var mapperType = typeof(TaskDefinitionEntity).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Mappings.DefinitionEntityMapper",
            throwOnError: true)!;
        var method = mapperType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == methodName &&
                method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType == parameter.GetType());

        try
        {
            return (TResult)method.Invoke(null, [parameter])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static TResult InvokePrivate<TResult>(
        string methodName,
        Type[] parameterTypes,
        object?[] arguments)
    {
        var mapperType = typeof(TaskDefinitionEntity).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Mappings.DefinitionEntityMapper",
            throwOnError: true)!;
        var method = mapperType.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null)!;

        try
        {
            return (TResult)method.Invoke(null, arguments)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static TaskDefinitionModel CreateTask(
        Id stageId,
        TaskKind kind,
        ITaskConfiguration configuration)
        => new()
        {
            Id = Id.New(),
            StageDefinitionId = stageId,
            Key = $"task.{kind.ToString().ToLowerInvariant()}",
            Name = $"{kind} Task",
            Order = 1,
            Kind = kind,
            ExecutionMode = TaskExecutionMode.Sequential,
            Configuration = configuration,
            RetryPolicy = CreateRetryPolicy(),
            DispatchType = TaskDispatchType.FireAndWaitCallback,
            IsEnabled = true
        };

    private static MessagingTaskConfiguration CreateMessagingConfiguration(Id elementId)
        => new()
        {
            Topic = "inventories.reserve",
            Version = new SemanticVersion(1, 0, 0),
            SchemaBinding = CreateSchemaBinding(SchemaContractKind.CommandRequest, elementId, "legacy-hash"),
            RequestSchemaBinding = CreateSchemaBinding(SchemaContractKind.CommandRequest, elementId, "request-hash"),
            ResponseSchemaBinding = CreateSchemaBinding(SchemaContractKind.CommandResponse, elementId, "response-hash"),
            HasRequestValidation = true,
            RequestValidation = CreateValidation("RequestRejected", "request-dsl"),
            HasResponseValidation = true,
            ResponseValidation = CreateValidation("ResponseRejected", "response-dsl")
        };

    private static SchemaBinding CreateSchemaBinding(
        SchemaContractKind kind,
        Id? elementId = null,
        string contentHash = "content-hash")
        => new()
        {
            Id = Id.New(),
            ElementType = ElementType.Task,
            ElementId = elementId ?? Id.New(),
            ContractId = Id.New(),
            ContractKey = $"{kind.ToString().ToLowerInvariant()}.contract",
            ContractVersion = new SemanticVersion(1, 0, 0),
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "knowl",
            ContractKind = kind,
            StrictMode = true,
            IsValidationEnabled = true,
            Snapshot = new Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot
            {
                ContractKind = kind,
                RegistryProviderId = "provider-id",
                RegistryProviderKey = "knowl",
                ContractId = "contract-id",
                ContractKey = $"{kind.ToString().ToLowerInvariant()}.contract",
                ContractVersion = "1.0.0",
                SchemaFormat = "ButterMorph",
                SchemaJson = """{"type":"object"}""",
                ContentHash = contentHash,
                SourceArtifactId = "artifact-id",
                ResolvedBy = "test",
                ResolvedAtUtc = DateTimeOffset.UtcNow
            }
        };

    private static ValidationDefinition CreateValidation(string errorCode, string dsl)
        => new()
        {
            Engine = EngineType.DSL,
            ErrorCode = errorCode,
            Configuration = new DslValidationConfiguration
            {
                Dsl = dsl,
                SchemaHash = "schema-hash",
                SemanticDiagnosticsJson = string.Empty
            }
        };

    private static RetryPolicy CreateRetryPolicy()
        => new()
        {
            MaxRetries = 2,
            StrategyType = RetryStrategyType.Fixed,
            Strategy = new FixedRetryStrategy { Delay = new Duration(TimeSpan.FromMilliseconds(10)) },
            RetryableErrorCodes = ["Transient"],
            StopOnNonRetryableError = true
        };

    private sealed class UnsupportedRetryStrategy : IRetryStrategy
    {
        public RetryStrategyType Type => RetryStrategyType.Fixed;
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

    private sealed class UnsupportedTimeoutBehaviorPolicy : ITimeoutBehaviorPolicy
    {
        public TimeoutBehavior Behavior => TimeoutBehavior.Fail;
    }

    private sealed class UnsupportedTaskConfiguration : ITaskConfiguration
    {
        public TaskKind Kind => TaskKind.Plugin;
    }

    private sealed class UnsupportedTriggerChannel : ITriggerChannel
    {
        public TriggerType TriggerType => TriggerType.Event;

        public SchemaBinding SchemaBinding { get; set; } = null!;
    }
}
