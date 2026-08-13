using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;
using TaskDefinitionModel = Krackend.Sagas.Orchestrations.Design.Core.TaskDefinition;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Mappings;

/// <summary>
/// Represents DefinitionEntityMapper.
/// </summary>
internal static class DefinitionEntityMapper
{
    /// <summary>
    /// Executes ToEntity.
    /// </summary>
    public static OrchestrationDefinitionEntity ToEntity(this OrchestrationDefinition definition)
    {
        return new OrchestrationDefinitionEntity
        {
            Id = definition.Id,
            Key = definition.Key,
            Name = definition.Name,
            Domain = definition.Domain,
            DomainId = definition.DomainId,
            IsActive = definition.IsActive,
            CreatedOnUtc = definition.CreatedOnUtc,
            CreatedBy = definition.CreatedBy,
            UpdatedOnUtc = definition.UpdatedOnUtc,
            UpdatedBy = definition.UpdatedBy,
            Description = definition.Description,
            OwnerTeam = definition.OwnerTeam,
            OwnerTeamId = definition.OwnerTeamId,
            Tags = definition.Tags ?? new List<string>(),
        };
    }

    /// <summary>
    /// Executes ToDefinition.
    /// </summary>
    public static OrchestrationDefinition ToDefinition(this OrchestrationDefinitionEntity entity)
    {
        return new OrchestrationDefinition
        {
            Id = entity.Id,
            Key = entity.Key,
            Name = entity.Name,
            Description = entity.Description,
            Domain = entity.Domain,
            DomainId = entity.DomainId,
            DomainDisplayName = entity.DomainRef?.DisplayName ?? entity.Domain ?? string.Empty,
            OwnerTeam = entity.OwnerTeam,
            OwnerTeamId = entity.OwnerTeamId,
            OwnerTeamDisplayName = entity.OwnerTeamRef?.DisplayName ?? entity.OwnerTeam ?? string.Empty,
            Tags = entity.Tags ?? new List<string>(),
            IsActive = entity.IsActive,
            CreatedOnUtc = entity.CreatedOnUtc,
            CreatedBy = entity.CreatedBy,
            UpdatedOnUtc = entity.UpdatedOnUtc,
            UpdatedBy = entity.UpdatedBy,
        };
    }

    /// <summary>
    /// Executes ToEntity.
    /// </summary>
    public static OrchestrationVersionEntity ToEntity(this OrchestrationVersion definition)
    {
        return new OrchestrationVersionEntity
        {
            Id = definition.Id,
            OrchestrationDefinitionId = definition.OrchestrationDefinitionId,
            Version = definition.Version.ToString(),
            Status = definition.Status,
            VersionLabel = definition.VersionLabel,
            Description = definition.Description,
            Checksum = definition.Checksum.ToString(),
            Notes = definition.Notes,
            CreatedOnUtc = definition.CreatedOnUtc,
            CreatedBy = definition.CreatedBy,
            ApprovedOnUtc = definition.ApprovedOnUtc,
            ApprovedBy = definition.ApprovedBy,
            UpdatedOnUtc = definition.UpdatedOnUtc,
            UpdatedBy = definition.UpdatedBy,
        };
    }

    /// <summary>
    /// Executes ToDefinition.
    /// </summary>
    public static OrchestrationVersion ToDefinition(this OrchestrationVersionEntity entity)
    {
        return new OrchestrationVersion
        {
            Id = entity.Id,
            OrchestrationDefinitionId = entity.OrchestrationDefinitionId,
            Version = ParseSemanticVersion(entity.Version),
            Status = entity.Status,
            VersionLabel = entity.VersionLabel,
            Description = entity.Description,
            Checksum = new Checksum(entity.Checksum),
            Notes = entity.Notes,
            CreatedOnUtc = entity.CreatedOnUtc,
            CreatedBy = entity.CreatedBy,
            ApprovedOnUtc = entity.ApprovedOnUtc,
            ApprovedBy = entity.ApprovedBy,
            UpdatedOnUtc = entity.UpdatedOnUtc,
            UpdatedBy = entity.UpdatedBy,
        };
    }

    /// <summary>
    /// Executes ToEntity.
    /// </summary>
    public static StageDefinitionEntity ToEntity(this StageDefinition definition)
    {
        return new StageDefinitionEntity
        {
            Id = definition.Id,
            OrchestrationVersionId = definition.OrchestrationVersionId,
            Key = definition.Key,
            Name = definition.Name,
            Order = definition.Order,
            Description = definition.Description,
            ExecutionCondition = ToOptionalJson(definition.ExecutionCondition, definition.HasExecutionCondition),
        };
    }

    /// <summary>
    /// Executes ToEntity.
    /// </summary>
    public static DomainEntity ToEntity(this Domain definition)
    {
        return new DomainEntity
        {
            Id = definition.Id,
            Key = definition.Key,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            IsActive = definition.IsActive,
            CreatedOnUtc = definition.CreatedOnUtc,
            UpdatedOnUtc = definition.UpdatedOnUtc,
        };
    }

    /// <summary>
    /// Executes ToDefinition.
    /// </summary>
    public static Domain ToDefinition(this DomainEntity entity)
    {
        return new Domain
        {
            Id = entity.Id,
            Key = entity.Key,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedOnUtc = entity.CreatedOnUtc,
            UpdatedOnUtc = entity.UpdatedOnUtc,
        };
    }

    /// <summary>
    /// Executes ToEntity.
    /// </summary>
    public static TeamProjectionEntity ToEntity(this TeamProjection definition)
    {
        return new TeamProjectionEntity
        {
            Id = definition.Id,
            Key = definition.Key,
            DisplayName = definition.DisplayName,
            IsActive = definition.IsActive,
            UpdatedAtUtc = definition.UpdatedAtUtc,
        };
    }

    /// <summary>
    /// Executes ToDefinition.
    /// </summary>
    public static TeamProjection ToDefinition(this TeamProjectionEntity entity)
    {
        return new TeamProjection
        {
            Id = entity.Id,
            Key = entity.Key,
            DisplayName = entity.DisplayName,
            IsActive = entity.IsActive,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }

    /// <summary>
    /// Executes ToDefinition.
    /// </summary>
    public static StageDefinition ToDefinition(this StageDefinitionEntity entity)
    {
        return new StageDefinition
        {
            Id = entity.Id,
            OrchestrationVersionId = entity.OrchestrationVersionId,
            Key = entity.Key,
            Name = entity.Name,
            Description = entity.Description,
            Order = entity.Order,
            ExecutionCondition = ToOptionalModel(entity.ExecutionCondition),
            HasExecutionCondition = IsExecutionConditionEnabled(entity.ExecutionCondition),
        };
    }

    /// <summary>
    /// Executes ToEntity.
    /// </summary>
    public static TaskDefinitionEntity ToEntity(this TaskDefinitionModel definition)
    {
        return new TaskDefinitionEntity
        {
            Id = definition.Id,
            StageDefinitionId = definition.StageDefinitionId,
            Key = definition.Key,
            Name = definition.Name,
            Order = definition.Order,
            Kind = definition.Kind,
            ExecutionMode = definition.ExecutionMode,
            ParallelGroupId = definition.ParallelGroupId,
            OnErrorPolicy = definition.OnErrorPolicy,
            DispatchType = definition.DispatchType,
            IsEnabled = definition.IsEnabled,
            Notes = definition.Notes,
            ExecutionCondition = ToOptionalJson(definition.ExecutionCondition, definition.HasExecutionCondition),
            Transformation = ToOptionalJson(definition.Transformation, definition.HasTransformation),
            Configuration = ToJson(definition.Configuration),
            RetryPolicy = ToOptionalJson(definition.RetryPolicy),
            TimeoutPolicy = ToOptionalJson(definition.TimeoutPolicy),
            CompensationDefinition = ToOptionalJson(definition.CompensationDefinition),
        };
    }

    /// <summary>
    /// Executes ToDefinition.
    /// </summary>
    public static TaskDefinitionModel ToDefinition(this TaskDefinitionEntity entity)
    {
        return new TaskDefinitionModel
        {
            Id = entity.Id,
            StageDefinitionId = entity.StageDefinitionId,
            Key = entity.Key,
            Name = entity.Name,
            Order = entity.Order,
            Notes = entity.Notes,
            Kind = entity.Kind,
            ExecutionMode = entity.ExecutionMode,
            ParallelGroupId = entity.ParallelGroupId,
            ExecutionCondition = ToOptionalModel(entity.ExecutionCondition),
            HasExecutionCondition = IsExecutionConditionEnabled(entity.ExecutionCondition),
            Transformation = ToOptionalModel(entity.Transformation),
            HasTransformation = IsTransformationEnabled(entity.Transformation),
            Configuration = ToModel(entity.Configuration),
            RetryPolicy = ToOptionalModel(entity.RetryPolicy),
            TimeoutPolicy = ToOptionalModel(entity.TimeoutPolicy),
            OnErrorPolicy = entity.OnErrorPolicy,
            CompensationDefinition = ToOptionalModel(entity.CompensationDefinition),
            DispatchType = entity.DispatchType,
            IsEnabled = entity.IsEnabled,
        };
    }

    /// <summary>
    /// Executes ToEntity.
    /// </summary>
    public static TriggerBindingEntity ToEntity(this TriggerBinding definition)
    {
        return new TriggerBindingEntity
        {
            Id = definition.Id,
            OrchestrationVersionId = definition.OrchestrationVersionId,
            Key = definition.Key,
            TriggerType = definition.TriggerType,
            IsEnabled = definition.IsEnabled,
            Description = definition.Description,
            TriggerChannel = ToJson(definition.TriggerChannel),
        };
    }

    /// <summary>
    /// Executes ToDefinition.
    /// </summary>
    public static TriggerBinding ToDefinition(this TriggerBindingEntity entity)
    {
        return new TriggerBinding
        {
            Id = entity.Id,
            OrchestrationVersionId = entity.OrchestrationVersionId,
            Key = entity.Key,
            TriggerType = entity.TriggerType,
            TriggerChannel = ToModel(entity.TriggerChannel),
            IsEnabled = entity.IsEnabled,
            Description = entity.Description,
        };
    }

    /// <summary>
    /// Executes ToEntity.
    /// </summary>
    public static VariableDefinitionEntity ToEntity(this VariableDefinition definition)
    {
        return new VariableDefinitionEntity
        {
            Id = definition.Id,
            OrchestrationVersionId = definition.OrchestrationVersionId,
            Key = definition.Key,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Scope = definition.Scope,
            ValueType = definition.ValueType,
            DefaultValue = definition.DefaultValue,
            IsRequired = definition.IsRequired,
            IsSensitive = definition.IsSensitive,
        };
    }

    /// <summary>
    /// Executes ToDefinition.
    /// </summary>
    public static VariableDefinition ToDefinition(this VariableDefinitionEntity entity)
    {
        return new VariableDefinition
        {
            Id = entity.Id,
            OrchestrationVersionId = entity.OrchestrationVersionId,
            Key = entity.Key,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            Scope = entity.Scope,
            ValueType = entity.ValueType,
            DefaultValue = entity.DefaultValue,
            IsRequired = entity.IsRequired,
            IsSensitive = entity.IsSensitive,
        };
    }

    /// <summary>
    /// Executes ToEntity.
    /// </summary>
    public static ParallelGroupDefinitionEntity ToEntity(this ParallelGroupDefinition definition)
    {
        return new ParallelGroupDefinitionEntity
        {
            Id = definition.Id,
            StageDefinitionId = definition.StageDefinitionId,
            Name = definition.Name,
            JoinPolicy = definition.JoinPolicy,
            MaxParallelAgents = definition.MaxParallelAgents,
        };
    }

    /// <summary>
    /// Executes ToDefinition.
    /// </summary>
    public static ParallelGroupDefinition ToDefinition(this ParallelGroupDefinitionEntity entity)
    {
        return new ParallelGroupDefinition
        {
            Id = entity.Id,
            StageDefinitionId = entity.StageDefinitionId,
            Name = entity.Name,
            JoinPolicy = entity.JoinPolicy,
            MaxParallelAgents = entity.MaxParallelAgents,
        };
    }

    /// <summary>
    /// Executes ToEntity.
    /// </summary>
    public static BranchRuleDefinitionEntity ToEntity(this BranchRuleDefinition definition, Id stageDefinitionId)
    {
        return new BranchRuleDefinitionEntity
        {
            Id = definition.Id,
            StageDefinitionId = stageDefinitionId,
            FromType = definition.FromType,
            FromId = definition.FromId,
            NavigateToType = definition.NavigateToType,
            NavigateToId = definition.NavigateToId,
            Condition = ToJson(definition.Condition),
        };
    }

    /// <summary>
    /// Executes ToDefinition.
    /// </summary>
    public static BranchRuleDefinition ToDefinition(this BranchRuleDefinitionEntity entity)
    {
        return new BranchRuleDefinition
        {
            Id = entity.Id,
            FromType = entity.FromType,
            FromId = entity.FromId,
            Condition = ToModel(entity.Condition),
            NavigateToType = entity.NavigateToType,
            NavigateToId = entity.NavigateToId,
        };
    }

    /// <summary>
    /// Executes ToJson.
    /// </summary>
    private static ExecutionConditionJsonModel ToJson(ExecutionCondition source)
        => new()
        {
            IsEnabled = true,
            Engine = source.Engine,
            Configuration = source.Configuration switch
            {
                DslConditionConfiguration dsl => new ConditionConfigurationEnvelopeJsonModel
                {
                    Type = "dsl",
                    Dsl = new DslConditionConfigurationJsonModel { Expression = dsl.Expression.ToString() },
                },
                _ => throw new NotSupportedException($"Condition configuration '{source.Configuration?.GetType().Name}' is not supported."),
            },
          };

    /// <summary>
    /// Executes ToOptionalJson.
    /// </summary>
    private static ExecutionConditionJsonModel ToOptionalJson(ExecutionCondition source)
        => source is null ? null : ToJson(source);

    /// <summary>
    /// Executes ToOptionalJson.
    /// </summary>
    private static ExecutionConditionJsonModel ToOptionalJson(ExecutionCondition source, bool isEnabled)
    {
        if (!isEnabled)
        {
            var json = source is null ? new ExecutionConditionJsonModel() : ToJson(source);
            json.IsEnabled = false;
            return json;
        }

        return source is null
            ? null
            : ToJson(source);
    }

    /// <summary>
    /// Executes ToModel.
    /// </summary>
    private static ExecutionCondition ToModel(ExecutionConditionJsonModel source)
    {
        var type = (source?.Configuration?.Type ?? string.Empty).Trim().ToLowerInvariant();
        var dslExpression = source?.Configuration?.Dsl?.Expression;
        var shouldUseDsl = type == "dsl" || !string.IsNullOrWhiteSpace(dslExpression);

        return new ExecutionCondition
        {
            Engine = source?.Engine ?? EngineType.DSL,
            Configuration = shouldUseDsl
                ? new DslConditionConfiguration { Expression = new Expression(dslExpression ?? string.Empty) }
                : new DslConditionConfiguration(),
        };
    }

    /// <summary>
    /// Executes ToJson.
    /// </summary>
    private static RetryPolicyJsonModel ToJson(RetryPolicy source)
        => new()
        {
            MaxRetries = source.MaxRetries,
            StrategyType = source.StrategyType,
            Strategy = source.Strategy switch
            {
                FixedRetryStrategy fixedStrategy => new RetryStrategyEnvelopeJsonModel
                {
                    Type = "fixed",
                    Fixed = new FixedRetryStrategyJsonModel { Delay = fixedStrategy.Delay.Value },
                },
                _ => throw new NotSupportedException($"Retry strategy '{source.Strategy?.GetType().Name}' is not supported."),
            },
            RetryableErrorCodes = source.RetryableErrorCodes ?? new List<string>(),
            StopOnNonRetryableError = source.StopOnNonRetryableError,
          };

    /// <summary>
    /// Executes ToOptionalJson.
    /// </summary>
    private static RetryPolicyJsonModel ToOptionalJson(RetryPolicy source)
        => source is null ? null : ToJson(source);

    /// <summary>
    /// Executes ToModel.
    /// </summary>
    private static RetryPolicy ToModel(RetryPolicyJsonModel source)
        => new()
        {
            MaxRetries = source?.MaxRetries ?? 0,
            StrategyType = source?.StrategyType ?? RetryStrategyType.Fixed,
            Strategy = (source?.Strategy?.Type ?? string.Empty).ToLowerInvariant() switch
            {
                "fixed" => new FixedRetryStrategy { Delay = new Duration(source.Strategy.Fixed?.Delay ?? TimeSpan.Zero) },
                _ => new FixedRetryStrategy(),
            },
            RetryableErrorCodes = source?.RetryableErrorCodes ?? new List<string>(),
            StopOnNonRetryableError = source?.StopOnNonRetryableError ?? false,
        };

    /// <summary>
    /// Executes ToJson.
    /// </summary>
    private static TimeoutPolicyJsonModel ToJson(TimeoutPolicy source)
        => new()
        {
            Timeout = source.Timeout.Value,
            TimeoutBehavior = source.TimeoutBehavior,
            TimeoutBehaviorPolicy = source.TimeoutBehaviorPolicy switch
            {
                FailTimeoutBehaviorPolicy fail => new TimeoutBehaviorPolicyEnvelopeJsonModel
                {
                    Type = "fail",
                    Fail = new FailTimeoutBehaviorPolicyJsonModel { ErrorCode = fail.ErrorCode },
                },
                WaitTimeoutBehaviorPolicy wait => new TimeoutBehaviorPolicyEnvelopeJsonModel
                {
                    Type = "wait",
                    Wait = new WaitTimeoutBehaviorPolicyJsonModel
                    {
                        OrchestrationAction = wait.OrchestrationAction,
                        WaitingTime = wait.WaitingTime.Value,
                    },
                },
                ReconcileTimeoutBehaviorPolicy reconcile => new TimeoutBehaviorPolicyEnvelopeJsonModel
                {
                    Type = "reconcile",
                    Reconcile = new ReconcileTimeoutBehaviorPolicyJsonModel
                    {
                        OrchestrationAction = reconcile.OrchestrationAction,
                        RetryPolicy = ToJson(reconcile.RetryPolicy),
                    },
                },
                _ => throw new NotSupportedException($"Timeout behavior policy '{source.TimeoutBehaviorPolicy?.GetType().Name}' is not supported."),
            },
          };

    /// <summary>
    /// Executes ToOptionalJson.
    /// </summary>
    private static TimeoutPolicyJsonModel ToOptionalJson(TimeoutPolicy source)
        => source is null ? null : ToJson(source);

    /// <summary>
    /// Executes ToModel.
    /// </summary>
    private static TimeoutPolicy ToModel(TimeoutPolicyJsonModel source)
        => new()
        {
            Timeout = new Duration(source?.Timeout ?? TimeSpan.Zero),
            TimeoutBehavior = source?.TimeoutBehavior ?? TimeoutBehavior.Fail,
            TimeoutBehaviorPolicy = (source?.TimeoutBehaviorPolicy?.Type ?? string.Empty).ToLowerInvariant() switch
            {
                "fail" => new FailTimeoutBehaviorPolicy { ErrorCode = source.TimeoutBehaviorPolicy.Fail?.ErrorCode },
                "wait" => new WaitTimeoutBehaviorPolicy
                {
                    OrchestrationAction = source.TimeoutBehaviorPolicy.Wait?.OrchestrationAction ?? OrchestrationActionOnTimeout.Block,
                    WaitingTime = new Duration(source.TimeoutBehaviorPolicy.Wait?.WaitingTime ?? TimeSpan.Zero),
                },
                "reconcile" => new ReconcileTimeoutBehaviorPolicy
                {
                    OrchestrationAction = source.TimeoutBehaviorPolicy.Reconcile?.OrchestrationAction ?? OrchestrationActionOnTimeout.Block,
                    RetryPolicy = ToModel(source.TimeoutBehaviorPolicy.Reconcile?.RetryPolicy),
                },
                _ => new FailTimeoutBehaviorPolicy(),
            },
        };

    /// <summary>
    /// Executes ToJson.
    /// </summary>
    private static TransformationDefinitionJsonModel ToJson(TransformationDefinition source)
        => new()
        {
            IsEnabled = true,
            Engine = source.Engine,
            Configuration = source.Configuration switch
            {
                DslTransformationConfiguration => new TransformationConfigurationEnvelopeJsonModel
                {
                    Type = "dsl",
                    Dsl = new DslTransformationConfigurationJsonModel(),
                },
                _ => throw new NotSupportedException($"Transformation configuration '{source.Configuration?.GetType().Name}' is not supported."),
            },
          };

    /// <summary>
    /// Executes ToOptionalJson.
    /// </summary>
    private static TransformationDefinitionJsonModel ToOptionalJson(TransformationDefinition source)
        => source is null ? null : ToJson(source);

    /// <summary>
    /// Executes ToOptionalJson.
    /// </summary>
    private static TransformationDefinitionJsonModel ToOptionalJson(TransformationDefinition source, bool isEnabled)
    {
        if (!isEnabled)
        {
            var json = source is null ? new TransformationDefinitionJsonModel() : ToJson(source);
            json.IsEnabled = false;
            return json;
        }

        return source is null
            ? null
            : ToJson(source);
    }

    /// <summary>
    /// Executes ToModel.
    /// </summary>
    private static TransformationDefinition ToModel(TransformationDefinitionJsonModel source)
        => new()
        {
            Engine = source?.Engine ?? EngineType.DSL,
            Configuration = (source?.Configuration?.Type ?? string.Empty).ToLowerInvariant() switch
            {
                "dsl" => new DslTransformationConfiguration(),
                _ => new DslTransformationConfiguration(),
            },
        };

    /// <summary>
    /// Executes ToJson.
    /// </summary>
    private static TaskConfigurationEnvelopeJsonModel ToJson(ITaskConfiguration source)
        => source switch
        {
            HttpTaskConfiguration http => new TaskConfigurationEnvelopeJsonModel
            {
                Type = "http",
                Http = new HttpTaskConfigurationJsonModel
                {
                SchemaBinding = ToJson(http.SchemaBinding, http.HasSchemaValidation),
                BaseUrlVariableRef = http.BaseUrlVariableRef,
                RelativePath = http.RelativePath,
                Method = http.Method,
                HeadersTemplateJson = http.HeadersTemplate?.ToJsonString(),
                QueryTemplateJson = http.QueryTemplate?.ToJsonString(),
                ExpectedStatusCodes = http.ExpectedStatusCodes ?? new List<int>(),
                AllowSyncResponse = http.AllowSyncResponse,
                },
            },
            MessagingTaskConfiguration messaging => new TaskConfigurationEnvelopeJsonModel
            {
                Type = "messaging",
                Messaging = new MessagingTaskConfigurationJsonModel
                {
                    Topic = messaging.Topic,
                    Version = messaging.Version.ToString(),
                    SchemaBinding = ToJson(messaging.SchemaBinding, messaging.HasSchemaValidation),
                },
            },
            PluginTaskConfiguration plugin => new TaskConfigurationEnvelopeJsonModel
            {
                Type = "plugin",
                Plugin = new PluginTaskConfigurationJsonModel
                {
                    PluginId = plugin.PluginId.ToString(),
                },
            },
            HumanApprovalTaskConfiguration => new TaskConfigurationEnvelopeJsonModel
            {
                Type = "humanApproval",
                HumanApproval = new HumanApprovalTaskConfigurationJsonModel(),
            },
            _ => throw new NotSupportedException($"Task configuration '{source?.GetType().Name}' is not supported."),
        };

    /// <summary>
    /// Executes ToModel.
    /// </summary>
    private static ITaskConfiguration ToModel(TaskConfigurationEnvelopeJsonModel source)
    {
        if (source is null)
        {
            return new HumanApprovalTaskConfiguration();
        }

        return (source.Type ?? string.Empty).ToLowerInvariant() switch
        {
            "http" => new HttpTaskConfiguration
            {
                SchemaBinding = ToModel(source.Http?.SchemaBinding),
                HasSchemaValidation = IsSchemaValidationEnabled(source.Http?.SchemaBinding),
                BaseUrlVariableRef = source.Http?.BaseUrlVariableRef,
                RelativePath = source.Http?.RelativePath,
                Method = source.Http?.Method,
                HeadersTemplate = string.IsNullOrWhiteSpace(source.Http?.HeadersTemplateJson) ? null : JsonNode.Parse(source.Http.HeadersTemplateJson),
                QueryTemplate = string.IsNullOrWhiteSpace(source.Http?.QueryTemplateJson) ? null : JsonNode.Parse(source.Http.QueryTemplateJson),
                ExpectedStatusCodes = source.Http?.ExpectedStatusCodes ?? new List<int>(),
                AllowSyncResponse = source.Http?.AllowSyncResponse ?? false,
            },
            "messaging" => new MessagingTaskConfiguration
            {
                Topic = source.Messaging?.Topic,
                Version = ParseSemanticVersion(source.Messaging?.Version),
                SchemaBinding = ToModel(source.Messaging?.SchemaBinding),
                HasSchemaValidation = IsSchemaValidationEnabled(source.Messaging?.SchemaBinding),
            },
            "plugin" => new PluginTaskConfiguration
            {
                PluginId = ParseId(source.Plugin?.PluginId),
            },
            _ => new HumanApprovalTaskConfiguration(),
        };
    }

    /// <summary>
    /// Executes ToJson.
    /// </summary>
    private static CompensationDefinitionJsonModel ToJson(CompensationDefinition source)
        => new()
        {
            CompensationTaskKind = source.CompensationTaskKind,
            HasTransformation = source.HasTransformation,
            HasExecutionCondition = source.HasExecutionCondition,
            Transformation = ToOptionalJson(source.Transformation, source.HasTransformation),
            ExecutionCondition = ToOptionalJson(source.ExecutionCondition, source.HasExecutionCondition),
            Configuration = ToJson(source.Configuration),
            RetryPolicy = ToOptionalJson(source.RetryPolicy),
            TimeoutPolicy = ToOptionalJson(source.TimeoutPolicy),
            DispatchType = source.DispatchType,
        };

    /// <summary>
    /// Executes ToOptionalJson.
    /// </summary>
    private static CompensationDefinitionJsonModel ToOptionalJson(CompensationDefinition source)
        => source is null ? null : ToJson(source);

    /// <summary>
    /// Executes ToModel.
    /// </summary>
    private static CompensationDefinition ToModel(CompensationDefinitionJsonModel source)
        => new()
        {
            CompensationTaskKind = source?.CompensationTaskKind ?? TaskKind.Http,
            Transformation = ToOptionalModel(source?.Transformation),
            HasTransformation = source?.HasTransformation ?? false,
            ExecutionCondition = ToOptionalModel(source?.ExecutionCondition),
            HasExecutionCondition = source?.HasExecutionCondition ?? false,
            Configuration = ToModel(source?.Configuration),
            RetryPolicy = ToOptionalModel(source?.RetryPolicy),
            TimeoutPolicy = ToOptionalModel(source?.TimeoutPolicy),
            DispatchType = source?.DispatchType ?? TaskDispatchType.FireAndForget,
        };

    /// <summary>
    /// Executes ToOptionalModel.
    /// </summary>
    private static ExecutionCondition ToOptionalModel(ExecutionConditionJsonModel source)
    {
        if (!IsExecutionConditionEnabled(source))
        {
            return null;
        }

        // Treat empty JSON payloads as no execution condition.
        var config = source.Configuration;
        var type = (config?.Type ?? string.Empty).Trim();
        var dslExpression = config?.Dsl?.Expression;
        if (config is null && string.IsNullOrWhiteSpace(type) && string.IsNullOrWhiteSpace(dslExpression))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(type) && string.IsNullOrWhiteSpace(dslExpression))
        {
            return null;
        }

        return ToModel(source);
    }

    /// <summary>
    /// Executes ToOptionalModel.
    /// </summary>
    private static TransformationDefinition ToOptionalModel(TransformationDefinitionJsonModel source)
        => IsTransformationEnabled(source) ? ToModel(source) : null;

    /// <summary>
    /// Executes ToOptionalModel.
    /// </summary>
    private static RetryPolicy ToOptionalModel(RetryPolicyJsonModel source)
        => source is null ? null : ToModel(source);

    /// <summary>
    /// Executes ToOptionalModel.
    /// </summary>
    private static TimeoutPolicy ToOptionalModel(TimeoutPolicyJsonModel source)
        => source is null ? null : ToModel(source);

    /// <summary>
    /// Executes ToOptionalModel.
    /// </summary>
    private static CompensationDefinition ToOptionalModel(CompensationDefinitionJsonModel source)
        => source is null ? null : ToModel(source);

    /// <summary>
    /// Executes ToJson.
    /// </summary>
    private static TriggerChannelEnvelopeJsonModel ToJson(ITriggerChannel source)
        => source switch
        {
            EventTriggerChannel evt => new TriggerChannelEnvelopeJsonModel
            {
                Type = "event",
                Event = new EventTriggerChannelJsonModel
                {
                    SchemaBinding = ToJson(evt.SchemaBinding, evt.HasSchemaValidation),
                    Topic = evt.Topic,
                    Version = evt.Version.ToString(),
                },
            },
            _ => throw new NotSupportedException($"Trigger channel '{source?.GetType().Name}' is not supported."),
        };

    /// <summary>
    /// Executes ToModel.
    /// </summary>
    private static ITriggerChannel ToModel(TriggerChannelEnvelopeJsonModel source)
        => (source?.Type ?? string.Empty).ToLowerInvariant() switch
        {
            "event" => new EventTriggerChannel
            {
                SchemaBinding = ToModel(source.Event?.SchemaBinding),
                HasSchemaValidation = IsSchemaValidationEnabled(source.Event?.SchemaBinding),
                Topic = source.Event?.Topic,
                Version = ParseSemanticVersion(source.Event?.Version),
            },
            _ => new EventTriggerChannel(),
        };

    /// <summary>
    /// Executes ToJson.
    /// </summary>
    private static SchemaBindingJsonModel ToJson(SchemaBinding source)
        => new()
        {
            Id = source.Id.ToString(),
            ElementType = source.ElementType,
            ElementId = source.ElementId.ToString(),
            ContractId = source.ContractId.ToString(),
            ContractKey = source.ContractKey,
            ContractVersion = source.ContractVersion.ToString(),
            RegistryProviderId = source.RegistryProviderId.ToString(),
            StrictMode = source.StrictMode,
            IsValidationEnabled = source.IsValidationEnabled,
        };

    /// <summary>
    /// Executes ToJson.
    /// </summary>
    private static SchemaBindingJsonModel ToJson(SchemaBinding source, bool isValidationEnabled)
    {
        if (!isValidationEnabled)
        {
            var disabledJson = source is null ? new SchemaBindingJsonModel() : ToJson(source);
            disabledJson.IsValidationEnabled = false;
            return disabledJson;
        }

        var json = source is null ? new SchemaBindingJsonModel() : ToJson(source);
        json.IsValidationEnabled = true;
        return json;
    }

    /// <summary>
    /// Executes ToModel.
    /// </summary>
    private static SchemaBinding ToModel(SchemaBindingJsonModel source)
        => new()
        {
            Id = ParseId(source?.Id),
            ElementType = source?.ElementType ?? ElementType.Task,
            ElementId = ParseId(source?.ElementId),
            ContractId = ParseId(source?.ContractId),
            ContractKey = source?.ContractKey ?? string.Empty,
            ContractVersion = ParseSemanticVersion(source?.ContractVersion),
            RegistryProviderId = ParseId(source?.RegistryProviderId),
            StrictMode = source?.StrictMode ?? false,
            IsValidationEnabled = IsSchemaValidationEnabled(source),
        };

    private static bool IsExecutionConditionEnabled(ExecutionConditionJsonModel source)
    {
        if (source is null)
        {
            return false;
        }

        return source.IsEnabled;
    }

    private static bool IsTransformationEnabled(TransformationDefinitionJsonModel source)
    {
        if (source is null)
        {
            return false;
        }

        return source.IsEnabled;
    }

    private static bool IsSchemaValidationEnabled(SchemaBindingJsonModel source)
    {
        if (source is null)
        {
            return false;
        }

        return source.IsValidationEnabled;
    }

    /// <summary>
    /// Executes ParseId.
    /// </summary>
    private static Id ParseId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? default : new Id(Ulid.Parse(value));
    }

    /// <summary>
    /// Executes ParseSemanticVersion.
    /// </summary>
    private static SemanticVersion ParseSemanticVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new SemanticVersion(0, 0, 0);
        }

        var parts = value.Split('.');
        if (parts.Length != 3)
        {
            throw new FormatException($"Invalid semantic version '{value}'.");
        }

        return new SemanticVersion(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }
}
