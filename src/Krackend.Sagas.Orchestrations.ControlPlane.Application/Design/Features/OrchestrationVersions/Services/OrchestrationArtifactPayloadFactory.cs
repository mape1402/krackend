using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Krackend.Sagas.Orchestrations.SchemaRegistry;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

public static class OrchestrationArtifactPayloadFactory
{
    public static string CreatePayloadJson(
        OrchestrationDefinition definition,
        OrchestrationVersion version)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(version);

        var artifact = new OrchestrationArtifact(
            definition.Id,
            version.Id,
            definition.Key,
            definition.Name,
            definition.Domain,
            version.Version,
            version.Checksum,
            version.TriggerBindings.Select(MapTrigger).ToArray(),
            version.VariableDefinitions.Select(MapVariable).ToArray(),
            version.StageDefinitions.OrderBy(x => x.Order).Select(MapStage).ToArray(),
            definition.Description,
            version.VersionLabel,
            version.Notes);

        return JsonSerializer.Serialize(artifact);
    }

    private static TriggerBindingArtifact MapTrigger(TriggerBinding trigger)
        => new(
            trigger.Id,
            trigger.TriggerType,
            MapTriggerChannel(trigger.TriggerChannel),
            trigger.IsEnabled,
            trigger.Description);

    private static ITriggerChannelArtifact MapTriggerChannel(ITriggerChannel channel)
        => channel switch
        {
            EventTriggerChannel eventChannel => new EventTriggerChannelArtifact(
                MapSchemaBinding(eventChannel.SchemaBinding, eventChannel.HasSchemaValidation, SchemaContractKind.Event),
                eventChannel.Topic,
                eventChannel.Version)
            {
                Validation = MapValidation(
                    eventChannel.Validation,
                    eventChannel.HasValidation || eventChannel.HasSchemaValidation,
                    "TriggerValidationFailed"),
            },
            _ => throw new InvalidOperationException($"Trigger channel '{channel.GetType().Name}' is not supported.")
        };

    private static VariableDefinitionArtifact MapVariable(VariableDefinition variable)
        => new(
            variable.Id,
            variable.Key,
            variable.DisplayName,
            variable.Description,
            variable.Scope,
            variable.ValueType,
            variable.DefaultValue,
            variable.IsRequired,
            variable.IsSensitive);

    private static StageArtifact MapStage(StageDefinition stage)
        => new(
            stage.Id,
            stage.Key,
            stage.Name,
            stage.Order,
            MapCondition(stage.ExecutionCondition, stage.HasExecutionCondition),
            stage.TaskDefinitions.OrderBy(x => x.Order).Select(MapTask).ToArray(),
            stage.ParallelGroups.Select(MapParallelGroup).ToArray(),
            stage.BranchRules.Select(MapBranchRule).ToArray(),
            stage.Description);

    private static ParallelGroupArtifact MapParallelGroup(ParallelGroupDefinition group)
        => new(
            group.Id,
            group.JoinPolicy,
            group.MaxParallelAgents);

    private static BranchRuleArtifact MapBranchRule(BranchRuleDefinition rule)
        => new(
            rule.Id,
            rule.FromType,
            rule.FromId,
            MapCondition(rule.Condition, isEnabled: true),
            rule.NavigateToType,
            rule.NavigateToId);

    private static TaskArtifact MapTask(TaskDefinition task)
        => new(
            task.Id,
            task.Key,
            task.Name,
            task.Order,
            task.Notes,
            task.Kind,
            task.ExecutionMode,
            task.ParallelGroupId,
            MapCondition(task.ExecutionCondition, task.HasExecutionCondition),
            MapTransformation(task.Transformation, task.HasTransformation),
            MapTaskConfiguration(task.Configuration),
            MapRetryPolicy(task.RetryPolicy),
            MapTimeoutPolicy(task.TimeoutPolicy),
            task.OnErrorPolicy,
            MapCompensation(task.CompensationDefinition),
            task.DispatchType,
            task.IsEnabled);

    private static CompensationArtifact MapCompensation(CompensationDefinition compensation)
        => new(
            compensation.CompensationTaskKind,
            MapTransformation(compensation.Transformation, compensation.HasTransformation),
            MapCondition(compensation.ExecutionCondition, compensation.HasExecutionCondition),
            MapTaskConfiguration(compensation.Configuration),
            MapRetryPolicy(compensation.RetryPolicy),
            MapTimeoutPolicy(compensation.TimeoutPolicy),
            compensation.DispatchType);

    private static ExecutionConditionArtifact MapCondition(ExecutionCondition condition, bool isEnabled)
    {
        if (condition is null)
        {
            return new ExecutionConditionArtifact(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(string.Empty)))
            {
                IsEnabled = false
            };
        }

        return new ExecutionConditionArtifact(condition.Engine, MapConditionConfiguration(condition.Configuration))
        {
            IsEnabled = isEnabled
        };
    }

    private static IConditionConfigurationArtifact MapConditionConfiguration(IConditionConfiguration configuration)
        => configuration switch
        {
            DslConditionConfiguration dsl => new DslConditionConfigurationArtifact(dsl.Expression),
            _ => throw new InvalidOperationException($"Condition configuration '{configuration.GetType().Name}' is not supported.")
        };

    private static TransformationArtifact MapTransformation(TransformationDefinition transformation, bool isEnabled)
    {
        if (transformation is null)
        {
            return new TransformationArtifact(EngineType.DSL, new DslTransformationConfigurationArtifact())
            {
                IsEnabled = false
            };
        }

        return new TransformationArtifact(transformation.Engine, MapTransformationConfiguration(transformation.Configuration))
        {
            IsEnabled = isEnabled
        };
    }

    private static ITransformationConfigurationArtifact MapTransformationConfiguration(ITransformationConfiguration configuration)
        => configuration switch
        {
            DslTransformationConfiguration dsl => new DslTransformationConfigurationArtifact
            {
                Dsl = dsl.Dsl,
                SourceContextHash = dsl.SourceContextHash,
                TargetSchemaHash = dsl.TargetSchemaHash,
                SemanticDiagnosticsJson = dsl.SemanticDiagnosticsJson,
            },
            _ => throw new InvalidOperationException($"Transformation configuration '{configuration.GetType().Name}' is not supported.")
        };

    private static ValidationArtifact MapValidation(
        ValidationDefinition validation,
        bool isEnabled,
        string fallbackErrorCode)
    {
        if (validation is null)
        {
            return new ValidationArtifact(EngineType.DSL, new DslValidationConfigurationArtifact())
            {
                IsEnabled = isEnabled,
                ErrorCode = fallbackErrorCode,
            };
        }

        return new ValidationArtifact(validation.Engine, MapValidationConfiguration(validation.Configuration))
        {
            IsEnabled = isEnabled,
            ErrorCode = string.IsNullOrWhiteSpace(validation.ErrorCode) ? fallbackErrorCode : validation.ErrorCode,
        };
    }

    private static IValidationConfigurationArtifact MapValidationConfiguration(IValidationConfiguration configuration)
        => configuration switch
        {
            DslValidationConfiguration dsl => new DslValidationConfigurationArtifact
            {
                Dsl = dsl.Dsl,
                SchemaHash = dsl.SchemaHash,
                SemanticDiagnosticsJson = dsl.SemanticDiagnosticsJson,
            },
            null => new DslValidationConfigurationArtifact(),
            _ => throw new InvalidOperationException($"Validation configuration '{configuration.GetType().Name}' is not supported.")
        };

    private static ITaskConfigurationArtifact MapTaskConfiguration(ITaskConfiguration configuration)
        => configuration switch
        {
            MessagingTaskConfiguration messaging => new MessagingTaskConfigurationArtifact(
                messaging.Topic,
                messaging.Version,
                MapSchemaBinding(
                    messaging.RequestSchemaBinding ?? messaging.SchemaBinding,
                    IsRequestValidationEnabled(messaging),
                    SchemaContractKind.CommandRequest))
            {
                RequestSchemaBinding = MapSchemaBinding(
                    messaging.RequestSchemaBinding ?? messaging.SchemaBinding,
                    IsRequestValidationEnabled(messaging),
                    SchemaContractKind.CommandRequest),
                ResponseSchemaBinding = MapSchemaBinding(
                    messaging.ResponseSchemaBinding,
                    IsResponseValidationEnabled(messaging),
                    SchemaContractKind.CommandResponse),
                RequestValidation = MapValidation(
                    messaging.RequestValidation,
                    IsRequestValidationEnabled(messaging),
                    "RequestValidationFailed"),
                ResponseValidation = MapValidation(
                    messaging.ResponseValidation,
                    IsResponseValidationEnabled(messaging),
                    "ResponseValidationFailed"),
            },
            HttpTaskConfiguration http => new HttpTaskConfigurationArtifact(
                MapSchemaBinding(http.SchemaBinding, http.HasSchemaValidation),
                http.BaseUrlVariableRef,
                http.RelativePath,
                http.Method,
                http.HeadersTemplate,
                http.QueryTemplate,
                http.ExpectedStatusCodes,
                http.AllowSyncResponse),
            PluginTaskConfiguration plugin => new PluginTaskConfigurationArtifact(plugin.PluginId),
            HumanApprovalTaskConfiguration => new HumanApprovalTaskConfigurationArtifact(),
            _ => throw new InvalidOperationException($"Task configuration '{configuration.GetType().Name}' is not supported.")
        };

    private static RetryPolicyArtifact MapRetryPolicy(RetryPolicy retryPolicy)
        => new(
            retryPolicy.MaxRetries,
            retryPolicy.StrategyType,
            MapRetryStrategy(retryPolicy.Strategy),
            retryPolicy.RetryableErrorCodes,
            retryPolicy.StopOnNonRetryableError);

    private static IRetryStrategyArtifact MapRetryStrategy(IRetryStrategy strategy)
        => strategy switch
        {
            FixedRetryStrategy fixedRetry => new FixedRetryStrategyArtifact(fixedRetry.Delay),
            _ => throw new InvalidOperationException($"Retry strategy '{strategy.GetType().Name}' is not supported.")
        };

    private static TimeoutPolicyArtifact MapTimeoutPolicy(TimeoutPolicy timeoutPolicy)
        => new(
            timeoutPolicy.Timeout,
            timeoutPolicy.TimeoutBehavior,
            MapTimeoutBehaviorPolicy(timeoutPolicy.TimeoutBehaviorPolicy));

    private static ITimeoutBehaviorPolicyArtifact MapTimeoutBehaviorPolicy(ITimeoutBehaviorPolicy policy)
        => policy switch
        {
            FailTimeoutBehaviorPolicy fail => new FailTimeoutBehaviorPolicyArtifact(fail.ErrorCode),
            WaitTimeoutBehaviorPolicy wait => new WaitTimeoutBehaviorPolicyArtifact(wait.OrchestrationAction, wait.WaitingTime),
            ReconcileTimeoutBehaviorPolicy reconcile => new ReconcileTimeoutBehaviorPolicyArtifact(
                reconcile.OrchestrationAction,
                MapRetryPolicy(reconcile.RetryPolicy)),
            _ => throw new InvalidOperationException($"Timeout behavior policy '{policy.GetType().Name}' is not supported.")
        };

    private static SchemaBindingArtifact MapSchemaBinding(SchemaBinding binding, bool isValidationEnabled)
        => MapSchemaBinding(binding, isValidationEnabled, binding?.ContractKind ?? SchemaContractKind.Unspecified);

    private static SchemaBindingArtifact MapSchemaBinding(SchemaBinding binding, bool isValidationEnabled, SchemaContractKind contractKind)
    {
        if (binding is null)
        {
            binding = new SchemaBinding
            {
                Id = Id.New(),
                ElementType = ElementType.Orchestration,
                ElementId = Id.New(),
                ContractId = Id.New(),
                ContractKey = string.Empty,
                ContractVersion = new SemanticVersion(0, 0, 0),
                RegistryProviderId = Id.New(),
                ContractKind = contractKind,
                StrictMode = false,
                IsValidationEnabled = false
            };
        }

        return new SchemaBindingArtifact(
            binding.Id,
            binding.ElementType,
            binding.ElementId,
            binding.ContractId,
            binding.ContractKey,
            binding.ContractVersion,
            binding.RegistryProviderId,
            binding.StrictMode)
        {
            IsValidationEnabled = isValidationEnabled && binding.IsValidationEnabled,
            RegistryProviderKey = binding.RegistryProviderKey,
            ContractKind = contractKind == SchemaContractKind.Unspecified ? binding.ContractKind : contractKind,
            Snapshot = MapSnapshot(binding, contractKind)
        };
    }

    private static SchemaContractSnapshotArtifact MapSnapshot(SchemaBinding binding, SchemaContractKind contractKind)
    {
        if (binding?.Snapshot is null)
        {
            return null;
        }

        return new SchemaContractSnapshotArtifact
        {
            ContractKind = contractKind == SchemaContractKind.Unspecified ? binding.Snapshot.ContractKind : contractKind,
            RegistryProviderId = binding.RegistryProviderId.ToString(),
            RegistryProviderKey = binding.RegistryProviderKey,
            ContractId = binding.ContractId.ToString(),
            ContractKey = binding.ContractKey,
            ContractVersion = binding.ContractVersion.ToString(),
            SchemaFormat = binding.Snapshot.SchemaFormat,
            SchemaJson = binding.Snapshot.SchemaJson,
            ContentHash = binding.Snapshot.ContentHash,
            ResolvedAtUtc = binding.Snapshot.ResolvedAtUtc,
        };
    }

    private static bool IsRequestValidationEnabled(MessagingTaskConfiguration configuration)
        => configuration?.HasRequestValidation == true || configuration?.HasSchemaValidation == true;

    private static bool IsResponseValidationEnabled(MessagingTaskConfiguration configuration)
        => configuration?.HasResponseValidation == true || configuration?.HasSchemaValidation == true;
}
