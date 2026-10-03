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
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Entities;
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
    public void EntityMapperCoversOptionalDefaultAndLegacyFallbackBranches()
    {
        var condition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("payload.active == true") }
        };
        var transformation = new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration { Dsl = "map payload" }
        };
        var validation = new ValidationDefinition
        {
            Engine = EngineType.DSL,
            ErrorCode = string.Empty,
            Configuration = null!
        };
        var schemaBinding = CreateSchemaBinding(SchemaContractKind.CommandResponse);

        var disabledConditionWithSource = InvokePrivate<ExecutionConditionJsonModel>(
            "ToOptionalJson",
            [typeof(ExecutionCondition), typeof(bool)],
            [condition, false]);
        var disabledConditionWithoutSource = InvokePrivate<ExecutionConditionJsonModel>(
            "ToOptionalJson",
            [typeof(ExecutionCondition), typeof(bool)],
            [null, false]);
        var enabledConditionWithoutSource = InvokePrivate<ExecutionConditionJsonModel?>(
            "ToOptionalJson",
            [typeof(ExecutionCondition), typeof(bool)],
            [null, true]);
        var conditionFromUnknownType = InvokePrivate<ExecutionCondition>(
            "ToModel",
            [typeof(ExecutionConditionJsonModel)],
            [
                new ExecutionConditionJsonModel
                {
                    IsEnabled = true,
                    Configuration = new ConditionConfigurationEnvelopeJsonModel
                    {
                        Type = "legacy"
                    }
                }
            ]);
        var optionalConditionFromBlankDsl = InvokePrivate<ExecutionCondition?>(
            "ToOptionalModel",
            [typeof(ExecutionConditionJsonModel)],
            [
                new ExecutionConditionJsonModel
                {
                    IsEnabled = true,
                    Configuration = new ConditionConfigurationEnvelopeJsonModel
                    {
                        Type = " ",
                        Dsl = new DslConditionConfigurationJsonModel { Expression = " " }
                    }
                }
            ]);

        var disabledTransformationWithSource = InvokePrivate<TransformationDefinitionJsonModel>(
            "ToOptionalJson",
            [typeof(TransformationDefinition), typeof(bool)],
            [transformation, false]);
        var disabledTransformationWithoutSource = InvokePrivate<TransformationDefinitionJsonModel>(
            "ToOptionalJson",
            [typeof(TransformationDefinition), typeof(bool)],
            [null, false]);
        var enabledTransformationWithoutSource = InvokePrivate<TransformationDefinitionJsonModel?>(
            "ToOptionalJson",
            [typeof(TransformationDefinition), typeof(bool)],
            [null, true]);
        var transformationFromLegacyEnvelope = InvokePrivate<TransformationDefinition>(
            "ToModel",
            [typeof(TransformationDefinitionJsonModel)],
            [
                new TransformationDefinitionJsonModel
                {
                    IsEnabled = true,
                    Configuration = new TransformationConfigurationEnvelopeJsonModel
                    {
                        Type = "legacy",
                        Dsl = new DslTransformationConfigurationJsonModel
                        {
                            Dsl = "legacy map",
                            SourceContextHash = null,
                            TargetSchemaHash = null,
                            SemanticDiagnosticsJson = "[]"
                        }
                    }
                }
            ]);

        var disabledValidationWithoutSource = InvokePrivate<ValidationDefinitionJsonModel>(
            "ToOptionalJson",
            [typeof(ValidationDefinition), typeof(bool), typeof(string)],
            [null, false, " "]);
        var enabledValidationWithoutSource = InvokePrivate<ValidationDefinitionJsonModel>(
            "ToOptionalJson",
            [typeof(ValidationDefinition), typeof(bool), typeof(string)],
            [null, true, "TriggerValidationFailed"]);
        var disabledValidationWithSource = InvokePrivate<ValidationDefinitionJsonModel>(
            "ToOptionalJson",
            [typeof(ValidationDefinition), typeof(bool), typeof(string)],
            [validation, false, "Ignored"]);

        var disabledNullSchemaBinding = InvokePrivate<SchemaBindingJsonModel>(
            "ToJson",
            [typeof(SchemaBinding), typeof(bool), typeof(SchemaContractKind)],
            [null, false, SchemaContractKind.CommandRequest]);
        var enabledNullSchemaBinding = InvokePrivate<SchemaBindingJsonModel>(
            "ToJson",
            [typeof(SchemaBinding), typeof(bool), typeof(SchemaContractKind)],
            [null, true, SchemaContractKind.Event]);
        var disabledSourceSchemaBinding = InvokePrivate<SchemaBindingJsonModel>(
            "ToJson",
            [typeof(SchemaBinding), typeof(bool), typeof(SchemaContractKind)],
            [schemaBinding, false, SchemaContractKind.CommandResponse]);
        var schemaBindingFromDefaults = InvokePrivate<SchemaBinding>(
            "ToModel",
            [typeof(SchemaBindingJsonModel)],
            [
                new SchemaBindingJsonModel
                {
                    Id = " ",
                    ElementId = null,
                    ContractId = null,
                    ContractVersion = null,
                    RegistryProviderId = null,
                    Snapshot = new SchemaContractSnapshotJsonModel()
                }
            ]);

        var waitTimeout = InvokePrivate<TimeoutPolicy>(
            "ToModel",
            [typeof(TimeoutPolicyJsonModel)],
            [
                new TimeoutPolicyJsonModel
                {
                    TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel { Type = "wait" }
                }
            ]);
        var reconcileTimeout = InvokePrivate<TimeoutPolicy>(
            "ToModel",
            [typeof(TimeoutPolicyJsonModel)],
            [
                new TimeoutPolicyJsonModel
                {
                    TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel { Type = "reconcile" }
                }
            ]);

        var httpDefaults = InvokePrivate<ITaskConfiguration>(
            "ToModel",
            [typeof(TaskConfigurationEnvelopeJsonModel)],
            [new TaskConfigurationEnvelopeJsonModel { Type = "http" }]);
        var pluginDefaults = InvokePrivate<ITaskConfiguration>(
            "ToModel",
            [typeof(TaskConfigurationEnvelopeJsonModel)],
            [new TaskConfigurationEnvelopeJsonModel { Type = "plugin" }]);
        var compensationDefaults = InvokePrivate<CompensationDefinition>(
            "ToModel",
            [typeof(CompensationDefinitionJsonModel)],
            [null]);
        var triggerDefaults = InvokePrivate<ITriggerChannel>(
            "ToModel",
            [typeof(TriggerChannelEnvelopeJsonModel)],
            [new TriggerChannelEnvelopeJsonModel { Type = "event" }]);

        Assert.False(disabledConditionWithSource.IsEnabled);
        Assert.False(disabledConditionWithoutSource.IsEnabled);
        Assert.Null(enabledConditionWithoutSource);
        Assert.IsType<DslConditionConfiguration>(conditionFromUnknownType.Configuration);
        Assert.Null(optionalConditionFromBlankDsl);
        Assert.False(disabledTransformationWithSource.IsEnabled);
        Assert.False(disabledTransformationWithoutSource.IsEnabled);
        Assert.Null(enabledTransformationWithoutSource);
        Assert.Equal("[]", Assert.IsType<DslTransformationConfiguration>(transformationFromLegacyEnvelope.Configuration).SemanticDiagnosticsJson);
        Assert.False(disabledValidationWithoutSource.IsEnabled);
        Assert.Equal("PayloadValidationFailed", disabledValidationWithoutSource.ErrorCode);
        Assert.True(enabledValidationWithoutSource.IsEnabled);
        Assert.Equal("TriggerValidationFailed", enabledValidationWithoutSource.ErrorCode);
        Assert.False(disabledValidationWithSource.IsEnabled);
        Assert.Equal("PayloadValidationFailed", disabledValidationWithSource.ErrorCode);
        Assert.False(disabledNullSchemaBinding.IsValidationEnabled);
        Assert.Equal(SchemaContractKind.CommandRequest, disabledNullSchemaBinding.ContractKind);
        Assert.True(enabledNullSchemaBinding.IsValidationEnabled);
        Assert.Equal(SchemaContractKind.Event, enabledNullSchemaBinding.ContractKind);
        Assert.False(disabledSourceSchemaBinding.IsValidationEnabled);
        Assert.Equal(SchemaContractKind.CommandResponse, disabledSourceSchemaBinding.ContractKind);
        Assert.Equal(default, schemaBindingFromDefaults.Id);
        Assert.Equal("ButterMorph", schemaBindingFromDefaults.Snapshot!.SchemaFormat);
        Assert.IsType<WaitTimeoutBehaviorPolicy>(waitTimeout.TimeoutBehaviorPolicy);
        Assert.IsType<ReconcileTimeoutBehaviorPolicy>(reconcileTimeout.TimeoutBehaviorPolicy);
        Assert.IsType<HttpTaskConfiguration>(httpDefaults);
        Assert.Equal(default, Assert.IsType<PluginTaskConfiguration>(pluginDefaults).PluginId);
        Assert.IsType<HumanApprovalTaskConfiguration>(compensationDefaults.Configuration);
        Assert.IsType<EventTriggerChannel>(triggerDefaults);
    }

    [Fact]
    public void EntityMapperCoversCatalogEventAndCompensationBranches()
    {
        var now = DateTime.UtcNow;
        var withRefs = ToDefinition(new OrchestrationDefinitionEntity
        {
            Id = Id.New(),
            Key = "orders",
            Name = "Orders",
            Domain = "legacy-domain",
            DomainId = Id.New(),
            DomainRef = new DomainEntity
            {
                Id = Id.New(),
                Key = "orders",
                DisplayName = "Orders domain",
                Description = "Domain ref",
                IsActive = true,
                CreatedOnUtc = now
            },
            OwnerTeam = "legacy-team",
            OwnerTeamId = Id.New(),
            OwnerTeamRef = new TeamEntity
            {
                Id = Id.New(),
                Key = "support",
                DisplayName = "Support team",
                Description = "Team ref",
                IsActive = true,
                CreatedOnUtc = now
            },
            Tags = ["retail"],
            IsActive = true,
            CreatedOnUtc = now,
            CreatedBy = "tester"
        });
        var withoutRefs = ToDefinition(new OrchestrationDefinitionEntity
        {
            Id = Id.New(),
            Key = "empty",
            Name = "Empty",
            Domain = null!,
            OwnerTeam = null!,
            Tags = null!,
            CreatedOnUtc = now,
            CreatedBy = "tester"
        });
        var metadataFallback = ToDefinition(new MetadataDescriptorEntity
        {
            Id = Id.New(),
            Key = "tenant",
            SourceKey = " ",
            DisplayName = "Tenant",
            Description = "Tenant metadata",
            SchemaJson = "{}",
            ContentHash = "hash",
            CreatedOnUtc = now
        });
        var metadataEntity = ToEntity(new MetadataDescriptor
        {
            Id = Id.New(),
            Key = "correlation",
            SourceKey = "x-correlation-id",
            DisplayName = "Correlation",
            Description = "Correlation metadata",
            SchemaJson = """{"type":"string"}""",
            ContentHash = "metadata-hash",
            CreatedOnUtc = now,
            UpdatedOnUtc = now
        });

        var trigger = new TriggerBinding
        {
            Id = Id.New(),
            OrchestrationVersionId = Id.New(),
            Key = "orders.created",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            Description = "Starts the order saga",
            TriggerChannel = new EventTriggerChannel
            {
                Topic = "orders.created",
                Version = new SemanticVersion(3, 2, 1),
                HasSchemaValidation = true,
                HasValidation = false,
                SchemaBinding = CreateSchemaBinding(SchemaContractKind.Event),
                Validation = null!
            },
            CompensationDefinition = new CompensationDefinition
            {
                CompensationTaskKind = TaskKind.Http,
                DispatchType = TaskDispatchType.FireAndWaitCallback,
                HasExecutionCondition = true,
                ExecutionCondition = new ExecutionCondition
                {
                    Engine = EngineType.DSL,
                    Configuration = new DslConditionConfiguration { Expression = new Expression("trigger.orderId != null") }
                },
                HasTransformation = true,
                Transformation = new TransformationDefinition
                {
                    Engine = EngineType.DSL,
                    Configuration = new DslTransformationConfiguration
                    {
                        Dsl = "map trigger compensation",
                        SemanticDiagnosticsJson = "[]"
                    }
                },
                Configuration = new HttpTaskConfiguration
                {
                    BaseUrlVariableRef = "ordersApi",
                    RelativePath = "/orders/{id}",
                    Method = "DELETE",
                    ExpectedStatusCodes = null,
                    HasSchemaValidation = false,
                    SchemaBinding = null
                },
                RetryPolicy = CreateRetryPolicy(),
                TimeoutPolicy = new TimeoutPolicy
                {
                    Timeout = new Duration(TimeSpan.FromSeconds(20)),
                    TimeoutBehavior = TimeoutBehavior.Reconcile,
                    TimeoutBehaviorPolicy = new ReconcileTimeoutBehaviorPolicy
                    {
                        OrchestrationAction = OrchestrationActionOnTimeout.Continue,
                        RetryPolicy = CreateRetryPolicy()
                    }
                }
            }
        };

        var triggerRoundTrip = ToDefinition(ToEntity(trigger));
        var eventChannel = Assert.IsType<EventTriggerChannel>(triggerRoundTrip.TriggerChannel);

        Assert.Equal("Orders domain", withRefs.DomainDisplayName);
        Assert.Equal("Support team", withRefs.OwnerTeamDisplayName);
        Assert.Equal(["retail"], withRefs.Tags);
        Assert.Equal(string.Empty, withoutRefs.DomainDisplayName);
        Assert.Equal(string.Empty, withoutRefs.OwnerTeamDisplayName);
        Assert.Empty(withoutRefs.Tags);
        Assert.Equal("tenant", metadataFallback.SourceKey);
        Assert.Equal("x-correlation-id", metadataEntity.SourceKey);
        Assert.Equal("orders.created", eventChannel.Topic);
        Assert.Equal("3.2.1", eventChannel.Version.ToString());
        Assert.True(eventChannel.HasSchemaValidation);
        Assert.True(eventChannel.HasValidation);
        Assert.Equal("TriggerValidationFailed", eventChannel.Validation!.ErrorCode);
        Assert.NotNull(triggerRoundTrip.CompensationDefinition);
        Assert.True(triggerRoundTrip.CompensationDefinition!.HasExecutionCondition);
        Assert.True(triggerRoundTrip.CompensationDefinition.HasTransformation);
        Assert.IsType<HttpTaskConfiguration>(triggerRoundTrip.CompensationDefinition.Configuration);
        Assert.IsType<ReconcileTimeoutBehaviorPolicy>(triggerRoundTrip.CompensationDefinition.TimeoutPolicy!.TimeoutBehaviorPolicy);
    }

    [Fact]
    public void EntityMapperCoversMessagingValidationAndNullDefaultBranches()
    {
        var enabledRequestValidation = new ValidationDefinitionJsonModel
        {
            IsEnabled = true,
            ErrorCode = "RequestRejected",
            Configuration = new ValidationConfigurationEnvelopeJsonModel
            {
                Type = "dsl",
                Dsl = new DslValidationConfigurationJsonModel { Dsl = "request" }
            }
        };
        var enabledResponseValidation = new ValidationDefinitionJsonModel
        {
            IsEnabled = true,
            ErrorCode = "ResponseRejected",
            Configuration = new ValidationConfigurationEnvelopeJsonModel
            {
                Type = "dsl",
                Dsl = new DslValidationConfigurationJsonModel { Dsl = "response" }
            }
        };
        var disabledRequestSchema = new SchemaBindingJsonModel
        {
            IsValidationEnabled = false,
            ContractKind = SchemaContractKind.CommandRequest
        };
        var enabledLegacySchema = new SchemaBindingJsonModel
        {
            IsValidationEnabled = true,
            ContractKind = SchemaContractKind.CommandRequest
        };
        var enabledResponseSchema = new SchemaBindingJsonModel
        {
            IsValidationEnabled = true,
            ContractKind = SchemaContractKind.CommandResponse
        };

        var nullMessaging = InvokePrivate<MessagingTaskConfiguration>(
            "ToMessagingTaskConfiguration",
            [typeof(MessagingTaskConfigurationJsonModel)],
            [null]);
        var validationMessaging = InvokePrivate<MessagingTaskConfiguration>(
            "ToMessagingTaskConfiguration",
            [typeof(MessagingTaskConfigurationJsonModel)],
            [
                new MessagingTaskConfigurationJsonModel
                {
                    Topic = "validation-only",
                    RequestValidation = enabledRequestValidation,
                    ResponseValidation = enabledResponseValidation
                }
            ]);
        var legacySchemaMessaging = InvokePrivate<MessagingTaskConfiguration>(
            "ToMessagingTaskConfiguration",
            [typeof(MessagingTaskConfigurationJsonModel)],
            [
                new MessagingTaskConfigurationJsonModel
                {
                    Topic = "legacy-schema",
                    SchemaBinding = enabledLegacySchema
                }
            ]);
        var explicitSchemaMessaging = InvokePrivate<MessagingTaskConfiguration>(
            "ToMessagingTaskConfiguration",
            [typeof(MessagingTaskConfigurationJsonModel)],
            [
                new MessagingTaskConfigurationJsonModel
                {
                    Topic = "explicit-schema",
                    SchemaBinding = enabledLegacySchema,
                    RequestSchemaBinding = disabledRequestSchema,
                    ResponseSchemaBinding = enabledResponseSchema
                }
            ]);

        var requestValidationFromNull = InvokePrivate<bool>(
            "IsRequestValidationEnabled",
            [typeof(MessagingTaskConfiguration)],
            [null]);
        var requestValidationFromFlag = InvokePrivate<bool>(
            "IsRequestValidationEnabled",
            [typeof(MessagingTaskConfiguration)],
            [new MessagingTaskConfiguration { Topic = "request", Version = new SemanticVersion(1, 0, 0), HasRequestValidation = true }]);
        var requestValidationFromSchemaFlag = InvokePrivate<bool>(
            "IsRequestValidationEnabled",
            [typeof(MessagingTaskConfiguration)],
            [new MessagingTaskConfiguration { Topic = "schema", Version = new SemanticVersion(1, 0, 0), HasSchemaValidation = true }]);
        var responseValidationFromNull = InvokePrivate<bool>(
            "IsResponseValidationEnabled",
            [typeof(MessagingTaskConfiguration)],
            [null]);
        var responseValidationFromFlag = InvokePrivate<bool>(
            "IsResponseValidationEnabled",
            [typeof(MessagingTaskConfiguration)],
            [new MessagingTaskConfiguration { Topic = "response", Version = new SemanticVersion(1, 0, 0), HasResponseValidation = true }]);
        var eventValidationFromNull = InvokePrivate<bool>(
            "IsEventValidationEnabled",
            [typeof(EventTriggerChannel)],
            [null]);
        var eventValidationFromSchemaFlag = InvokePrivate<bool>(
            "IsEventValidationEnabled",
            [typeof(EventTriggerChannel)],
            [new EventTriggerChannel { HasSchemaValidation = true }]);

        var defaultTimeout = InvokePrivate<TimeoutPolicy>(
            "ToModel",
            [typeof(TimeoutPolicyJsonModel)],
            [null]);
        var defaultTransformation = InvokePrivate<TransformationDefinition>(
            "ToModel",
            [typeof(TransformationDefinitionJsonModel)],
            [null]);
        var defaultValidation = InvokePrivate<ValidationDefinition>(
            "ToModel",
            [typeof(ValidationDefinitionJsonModel)],
            [null]);
        var defaultSchemaBinding = InvokePrivate<SchemaBinding>(
            "ToModel",
            [typeof(SchemaBindingJsonModel)],
            [null]);
        var defaultEvent = InvokePrivate<EventTriggerChannel>(
            "ToEventTriggerChannel",
            [typeof(EventTriggerChannelJsonModel)],
            [null]);

        Assert.False(nullMessaging.HasRequestValidation);
        Assert.True(validationMessaging.HasRequestValidation);
        Assert.True(validationMessaging.HasResponseValidation);
        Assert.True(legacySchemaMessaging.HasRequestValidation);
        Assert.False(explicitSchemaMessaging.HasRequestValidation);
        Assert.True(explicitSchemaMessaging.HasResponseValidation);
        Assert.False(requestValidationFromNull);
        Assert.True(requestValidationFromFlag);
        Assert.True(requestValidationFromSchemaFlag);
        Assert.False(responseValidationFromNull);
        Assert.True(responseValidationFromFlag);
        Assert.False(eventValidationFromNull);
        Assert.True(eventValidationFromSchemaFlag);
        Assert.IsType<FailTimeoutBehaviorPolicy>(defaultTimeout.TimeoutBehaviorPolicy);
        Assert.Equal(string.Empty, Assert.IsType<DslTransformationConfiguration>(defaultTransformation.Configuration).Dsl);
        Assert.Equal("PayloadValidationFailed", defaultValidation.ErrorCode);
        Assert.Equal(default, defaultSchemaBinding.Id);
        Assert.Equal(new SemanticVersion(0, 0, 0), defaultEvent.Version);
    }

    [Fact]
    public void EntityMapperCoversDefinitionEntityAndUnsupportedNullConfigurationBranches()
    {
        var definitionEntity = ToEntity(new OrchestrationDefinition
        {
            Id = Id.New(),
            Key = "orders",
            Name = "Orders",
            Domain = "retail",
            DomainId = Id.New(),
            IsActive = true,
            Tags = null!,
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tester"
        });
        var metadata = ToDefinition(new MetadataDescriptorEntity
        {
            Id = Id.New(),
            Key = "tenant",
            SourceKey = "tenant-header",
            DisplayName = "Tenant",
            SchemaJson = "{}",
            ContentHash = "hash",
            CreatedOnUtc = DateTime.UtcNow
        });
        var condition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("payload.ok") }
        };
        var transformation = new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration { Dsl = "map" }
        };
        var optionalCondition = InvokePrivate<ExecutionConditionJsonModel>(
            "ToOptionalJson",
            [typeof(ExecutionCondition)],
            [condition]);
        var optionalTransformation = InvokePrivate<TransformationDefinitionJsonModel>(
            "ToOptionalJson",
            [typeof(TransformationDefinition)],
            [transformation]);
        var fixedRetryDefaults = InvokePrivate<RetryPolicy>(
            "ToModel",
            [typeof(RetryPolicyJsonModel)],
            [
                new RetryPolicyJsonModel
                {
                    Strategy = new RetryStrategyEnvelopeJsonModel { Type = "fixed", Fixed = null }
                }
            ]);
        var failTimeoutDefaults = InvokePrivate<TimeoutPolicy>(
            "ToModel",
            [typeof(TimeoutPolicyJsonModel)],
            [
                new TimeoutPolicyJsonModel
                {
                    TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel { Type = "fail", Fail = null }
                }
            ]);
        var anyValidationFalse = InvokePrivate<bool>(
            "HasAnyMessagingValidation",
            [typeof(MessagingTaskConfiguration)],
            [new MessagingTaskConfiguration { Topic = "none", Version = new SemanticVersion(1, 0, 0) }]);
        var responseValidationFromSchemaFlag = InvokePrivate<bool>(
            "IsResponseValidationEnabled",
            [typeof(MessagingTaskConfiguration)],
            [new MessagingTaskConfiguration { Topic = "response-schema", Version = new SemanticVersion(1, 0, 0), HasSchemaValidation = true }]);
        var eventValidationFromFlag = InvokePrivate<bool>(
            "IsEventValidationEnabled",
            [typeof(EventTriggerChannel)],
            [new EventTriggerChannel { HasValidation = true }]);

        var conditionException = Assert.Throws<NotSupportedException>(() => InvokePrivate<ExecutionConditionJsonModel>(
            "ToJson",
            [typeof(ExecutionCondition)],
            [new ExecutionCondition { Engine = EngineType.DSL, Configuration = null! }]));
        var retryException = Assert.Throws<NotSupportedException>(() => InvokePrivate<RetryPolicyJsonModel>(
            "ToJson",
            [typeof(RetryPolicy)],
            [new RetryPolicy { StrategyType = RetryStrategyType.Fixed, Strategy = null! }]));
        var timeoutException = Assert.Throws<NotSupportedException>(() => InvokePrivate<TimeoutPolicyJsonModel>(
            "ToJson",
            [typeof(TimeoutPolicy)],
            [new TimeoutPolicy { TimeoutBehaviorPolicy = null! }]));
        var transformationException = Assert.Throws<NotSupportedException>(() => InvokePrivate<TransformationDefinitionJsonModel>(
            "ToJson",
            [typeof(TransformationDefinition)],
            [new TransformationDefinition { Engine = EngineType.DSL, Configuration = null! }]));
        var taskException = Assert.Throws<NotSupportedException>(() => InvokePrivate<TaskConfigurationEnvelopeJsonModel>(
            "ToJson",
            [typeof(ITaskConfiguration)],
            [null]));
        var triggerException = Assert.Throws<NotSupportedException>(() => InvokePrivate<TriggerChannelEnvelopeJsonModel>(
            "ToJson",
            [typeof(ITriggerChannel)],
            [null]));

        Assert.Empty(definitionEntity.Tags);
        Assert.Equal("tenant-header", metadata.SourceKey);
        Assert.True(optionalCondition.IsEnabled);
        Assert.True(optionalTransformation.IsEnabled);
        Assert.IsType<FixedRetryStrategy>(fixedRetryDefaults.Strategy);
        Assert.IsType<FailTimeoutBehaviorPolicy>(failTimeoutDefaults.TimeoutBehaviorPolicy);
        Assert.False(anyValidationFalse);
        Assert.True(responseValidationFromSchemaFlag);
        Assert.True(eventValidationFromFlag);
        Assert.Contains("Condition configuration", conditionException.Message, StringComparison.Ordinal);
        Assert.Contains("Retry strategy", retryException.Message, StringComparison.Ordinal);
        Assert.Contains("Timeout behavior policy", timeoutException.Message, StringComparison.Ordinal);
        Assert.Contains("Transformation configuration", transformationException.Message, StringComparison.Ordinal);
        Assert.Contains("Task configuration", taskException.Message, StringComparison.Ordinal);
        Assert.Contains("Trigger channel", triggerException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityMapperCoversRemainingNullAndExplicitEnvelopeBranches()
    {
        var defaultCondition = InvokePrivate<ExecutionCondition>(
            "ToModel",
            [typeof(ExecutionConditionJsonModel)],
            [null]);
        var explicitDslCondition = InvokePrivate<ExecutionCondition>(
            "ToModel",
            [typeof(ExecutionConditionJsonModel)],
            [
                new ExecutionConditionJsonModel
                {
                    Configuration = new ConditionConfigurationEnvelopeJsonModel
                    {
                        Type = "dsl",
                        Dsl = new DslConditionConfigurationJsonModel()
                    }
                }
            ]);
        var fixedRetryDefaults = InvokePrivate<RetryPolicy>(
            "ToModel",
            [typeof(RetryPolicyJsonModel)],
            [
                new RetryPolicyJsonModel
                {
                    Strategy = null
                }
            ]);
        var retryJsonWithNullCodes = InvokePrivate<RetryPolicyJsonModel>(
            "ToJson",
            [typeof(RetryPolicy)],
            [
                new RetryPolicy
                {
                    MaxRetries = 1,
                    StrategyType = RetryStrategyType.Fixed,
                    Strategy = new FixedRetryStrategy { Delay = Duration.FromSeconds(2) },
                    RetryableErrorCodes = null!,
                    StopOnNonRetryableError = true
                }
            ]);
        var failTimeoutWithError = InvokePrivate<TimeoutPolicy>(
            "ToModel",
            [typeof(TimeoutPolicyJsonModel)],
            [
                new TimeoutPolicyJsonModel
                {
                    TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel
                    {
                        Type = "fail",
                        Fail = new FailTimeoutBehaviorPolicyJsonModel { ErrorCode = "TimedOut" }
                    }
                }
            ]);
        var waitTimeoutDefaults = InvokePrivate<TimeoutPolicy>(
            "ToModel",
            [typeof(TimeoutPolicyJsonModel)],
            [
                new TimeoutPolicyJsonModel
                {
                    TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel
                    {
                        Type = "wait",
                        Wait = null
                    }
                }
            ]);
        var reconcileTimeoutDefaults = InvokePrivate<TimeoutPolicy>(
            "ToModel",
            [typeof(TimeoutPolicyJsonModel)],
            [
                new TimeoutPolicyJsonModel
                {
                    TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel
                    {
                        Type = "reconcile",
                        Reconcile = null
                    }
                }
            ]);
        var explicitDslTransformation = InvokePrivate<TransformationDefinition>(
            "ToModel",
            [typeof(TransformationDefinitionJsonModel)],
            [
                new TransformationDefinitionJsonModel
                {
                    Configuration = new TransformationConfigurationEnvelopeJsonModel
                    {
                        Type = "dsl",
                        Dsl = new DslTransformationConfigurationJsonModel
                        {
                            Dsl = "map explicit",
                            SemanticDiagnosticsJson = "[]"
                        }
                    }
                }
            ]);
        var defaultTransformation = InvokePrivate<TransformationDefinition>(
            "ToModel",
            [typeof(TransformationDefinitionJsonModel)],
            [null]);
        var legacyTransformation = InvokePrivate<TransformationDefinition>(
            "ToModel",
            [typeof(TransformationDefinitionJsonModel)],
            [
                new TransformationDefinitionJsonModel
                {
                    Engine = EngineType.Custom,
                    Configuration = new TransformationConfigurationEnvelopeJsonModel
                    {
                        Type = "legacy",
                        Dsl = new DslTransformationConfigurationJsonModel
                        {
                            Dsl = "map legacy",
                            SourceContextHash = "legacy-source",
                            TargetSchemaHash = "legacy-target",
                            SemanticDiagnosticsJson = " "
                        }
                    }
                }
            ]);
        var fallbackTransformation = InvokePrivate<TransformationDefinition>(
            "ToModel",
            [typeof(TransformationDefinitionJsonModel)],
            [
                new TransformationDefinitionJsonModel
                {
                    Configuration = new TransformationConfigurationEnvelopeJsonModel
                    {
                        Type = "custom",
                        Dsl = null
                    }
                }
            ]);
        var explicitDslValidation = InvokePrivate<ValidationDefinition>(
            "ToModel",
            [typeof(ValidationDefinitionJsonModel)],
            [
                new ValidationDefinitionJsonModel
                {
                    ErrorCode = "Explicit",
                    Configuration = new ValidationConfigurationEnvelopeJsonModel
                    {
                        Type = "dsl",
                        Dsl = new DslValidationConfigurationJsonModel
                        {
                            Dsl = "validate explicit",
                            SemanticDiagnosticsJson = "[]"
                        }
                    }
                }
            ]);
        var defaultValidation = InvokePrivate<ValidationDefinition>(
            "ToModel",
            [typeof(ValidationDefinitionJsonModel)],
            [null]);
        var legacyValidation = InvokePrivate<ValidationDefinition>(
            "ToModel",
            [typeof(ValidationDefinitionJsonModel)],
            [
                new ValidationDefinitionJsonModel
                {
                    Engine = EngineType.Custom,
                    Configuration = new ValidationConfigurationEnvelopeJsonModel
                    {
                        Type = "legacy",
                        Dsl = new DslValidationConfigurationJsonModel
                        {
                            Dsl = "validate legacy",
                            SchemaHash = "legacy-schema",
                            SemanticDiagnosticsJson = " "
                        }
                    }
                }
            ]);
        var fallbackValidation = InvokePrivate<ValidationDefinition>(
            "ToModel",
            [typeof(ValidationDefinitionJsonModel)],
            [
                new ValidationDefinitionJsonModel
                {
                    ErrorCode = " ",
                    Configuration = new ValidationConfigurationEnvelopeJsonModel
                    {
                        Type = "custom",
                        Dsl = null
                    }
                }
            ]);
        var noValidationMessagingJson = InvokePrivate<TaskConfigurationEnvelopeJsonModel>(
            "ToJson",
            [typeof(ITaskConfiguration)],
            [
                new MessagingTaskConfiguration
                {
                    Topic = "no.validation",
                    Version = new SemanticVersion(1, 0, 0),
                    SchemaBinding = null,
                    RequestSchemaBinding = null,
                    ResponseSchemaBinding = null,
                    HasRequestValidation = false,
                    HasResponseValidation = false
                }
            ]);
        var defaultTask = InvokePrivate<ITaskConfiguration>(
            "ToModel",
            [typeof(TaskConfigurationEnvelopeJsonModel)],
            [
                new TaskConfigurationEnvelopeJsonModel()
            ]);
        var explicitHttpTask = InvokePrivate<ITaskConfiguration>(
            "ToModel",
            [typeof(TaskConfigurationEnvelopeJsonModel)],
            [
                new TaskConfigurationEnvelopeJsonModel
                {
                    Type = "http",
                    Http = new HttpTaskConfigurationJsonModel
                    {
                        Method = "PATCH",
                        ExpectedStatusCodes = [204]
                    }
                }
            ]);
        var explicitPluginTask = InvokePrivate<ITaskConfiguration>(
            "ToModel",
            [typeof(TaskConfigurationEnvelopeJsonModel)],
            [
                new TaskConfigurationEnvelopeJsonModel
                {
                    Type = "plugin",
                    Plugin = new PluginTaskConfigurationJsonModel { PluginId = Id.New().ToString() }
                }
            ]);
        var explicitApprovalTask = InvokePrivate<ITaskConfiguration>(
            "ToModel",
            [typeof(TaskConfigurationEnvelopeJsonModel)],
            [
                new TaskConfigurationEnvelopeJsonModel
                {
                    Type = "humanApproval",
                    HumanApproval = new HumanApprovalTaskConfigurationJsonModel()
                }
            ]);
        var eventTrigger = InvokePrivate<ITriggerChannel>(
            "ToModel",
            [typeof(TriggerChannelEnvelopeJsonModel)],
            [
                new TriggerChannelEnvelopeJsonModel
                {
                    Type = "event",
                    Event = new EventTriggerChannelJsonModel
                    {
                        SchemaBinding = new SchemaBindingJsonModel { IsValidationEnabled = true },
                        Validation = new ValidationDefinitionJsonModel { IsEnabled = false },
                        Version = "2.3.4"
                    }
                }
            ]);
        var snapshotDefaults = InvokePrivate<DesignSchemaContractSnapshot>(
            "ToModel",
            [typeof(SchemaContractSnapshotJsonModel)],
            [
                new SchemaContractSnapshotJsonModel()
            ]);

        Assert.Null(Assert.IsType<DslConditionConfiguration>(defaultCondition.Configuration).Expression.Value);
        Assert.Equal(string.Empty, Assert.IsType<DslConditionConfiguration>(explicitDslCondition.Configuration).Expression.Value);
        Assert.IsType<FixedRetryStrategy>(fixedRetryDefaults.Strategy);
        Assert.Empty(retryJsonWithNullCodes.RetryableErrorCodes);
        Assert.Equal("TimedOut", Assert.IsType<FailTimeoutBehaviorPolicy>(failTimeoutWithError.TimeoutBehaviorPolicy).ErrorCode);
        Assert.Equal(OrchestrationActionOnTimeout.Block, Assert.IsType<WaitTimeoutBehaviorPolicy>(waitTimeoutDefaults.TimeoutBehaviorPolicy).OrchestrationAction);
        Assert.Equal(OrchestrationActionOnTimeout.Block, Assert.IsType<ReconcileTimeoutBehaviorPolicy>(reconcileTimeoutDefaults.TimeoutBehaviorPolicy).OrchestrationAction);
        Assert.Equal("map explicit", Assert.IsType<DslTransformationConfiguration>(explicitDslTransformation.Configuration).Dsl);
        Assert.Equal(EngineType.DSL, defaultTransformation.Engine);
        Assert.Equal("map legacy", Assert.IsType<DslTransformationConfiguration>(legacyTransformation.Configuration).Dsl);
        Assert.Equal("{}", Assert.IsType<DslTransformationConfiguration>(legacyTransformation.Configuration).SemanticDiagnosticsJson);
        Assert.Equal(string.Empty, Assert.IsType<DslTransformationConfiguration>(fallbackTransformation.Configuration).Dsl);
        Assert.Equal("Explicit", explicitDslValidation.ErrorCode);
        Assert.Equal("PayloadValidationFailed", defaultValidation.ErrorCode);
        Assert.Equal("validate legacy", Assert.IsType<DslValidationConfiguration>(legacyValidation.Configuration).Dsl);
        Assert.Equal("{}", Assert.IsType<DslValidationConfiguration>(legacyValidation.Configuration).SemanticDiagnosticsJson);
        Assert.Equal("PayloadValidationFailed", fallbackValidation.ErrorCode);
        Assert.Equal(string.Empty, Assert.IsType<DslValidationConfiguration>(fallbackValidation.Configuration).Dsl);
        Assert.False(noValidationMessagingJson.Messaging!.RequestSchemaBinding!.IsValidationEnabled);
        Assert.False(noValidationMessagingJson.Messaging.ResponseSchemaBinding!.IsValidationEnabled);
        Assert.IsType<HumanApprovalTaskConfiguration>(defaultTask);
        Assert.Equal([204], Assert.IsType<HttpTaskConfiguration>(explicitHttpTask).ExpectedStatusCodes);
        Assert.NotEqual(default, Assert.IsType<PluginTaskConfiguration>(explicitPluginTask).PluginId);
        Assert.IsType<HumanApprovalTaskConfiguration>(explicitApprovalTask);
        var eventChannel = Assert.IsType<EventTriggerChannel>(eventTrigger);
        Assert.True(eventChannel.HasSchemaValidation);
        Assert.True(eventChannel.HasValidation);
        Assert.Equal(new SemanticVersion(2, 3, 4), eventChannel.Version);
        Assert.Equal("ButterMorph", snapshotDefaults.SchemaFormat);
        Assert.Equal("{}", snapshotDefaults.SchemaJson);
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

    private static OrchestrationDefinition ToDefinition(OrchestrationDefinitionEntity entity)
        => Invoke<OrchestrationDefinition>("ToDefinition", entity);

    private static MetadataDescriptor ToDefinition(MetadataDescriptorEntity entity)
        => Invoke<MetadataDescriptor>("ToDefinition", entity);

    private static MetadataDescriptorEntity ToEntity(MetadataDescriptor descriptor)
        => Invoke<MetadataDescriptorEntity>("ToEntity", descriptor);

    private static OrchestrationDefinitionEntity ToEntity(OrchestrationDefinition definition)
        => Invoke<OrchestrationDefinitionEntity>("ToEntity", definition);

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
