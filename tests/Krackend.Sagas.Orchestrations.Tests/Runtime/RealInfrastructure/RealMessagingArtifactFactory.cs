namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.SchemaRegistry;

internal static class RealMessagingArtifactFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static RuntimeArtifactDeliveryPackage CreatePackage(OrchestrationArtifact artifact)
        => new()
        {
            ReleaseTargetId = Id.New().ToString(),
            ArtifactId = Id.New().ToString(),
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0",
            OrchestrationDefinitionId = artifact.OrchestrationDefinitionId.ToString(),
            OrchestrationVersionId = artifact.OrchestrationVersionId.ToString(),
            OrchestrationDefinitionKey = artifact.Key,
            Version = artifact.Version.ToString(),
            Checksum = artifact.Checksum.Value,
            PayloadJson = JsonSerializer.Serialize(artifact, SerializerOptions),
            CorrelationId = Id.New().ToString(),
            PromotedBy = "real-infra-e2e",
            PromotedOnUtc = DateTime.UtcNow
        };

    public static OrchestrationArtifact Artifact(
        string key,
        SemanticVersion version,
        string checksum,
        IReadOnlyList<TriggerBindingArtifact> triggers,
        params StageArtifact[] stages)
        => new(
            Id.New(),
            Id.New(),
            key,
            key,
            "real-infra",
            version,
            new Checksum(checksum),
            triggers,
            [],
            stages);

    public static TriggerBindingArtifact EventTrigger(string topic, SemanticVersion version)
    {
        var schemaBinding = new SchemaBindingArtifact(
            Id.New(),
            ElementType.Orchestration,
            Id.New(),
            Id.New(),
            topic,
            version,
            Id.New(),
            false)
        {
            ContractKind = SchemaContractKind.Event
        };

        return new TriggerBindingArtifact(
            Id.New(),
            TriggerType.Event,
            new EventTriggerChannelArtifact(schemaBinding, topic, version),
            true,
            $"Trigger {topic}");
    }

    public static StageArtifact Stage(
        string key,
        int order,
        params TaskArtifact[] tasks)
        => new(Id.New(), key, key, order, DisabledCondition(), tasks, [], []);

    public static StageArtifact StageWithGraph(
        string key,
        int order,
        IReadOnlyList<ParallelGroupArtifact> parallelGroups,
        IReadOnlyList<BranchRuleArtifact> branchRules,
        params TaskArtifact[] tasks)
        => new(Id.New(), key, key, order, DisabledCondition(), tasks, parallelGroups, branchRules);

    public static StageArtifact StageWithGraph(
        Id id,
        string key,
        int order,
        IReadOnlyList<ParallelGroupArtifact> parallelGroups,
        IReadOnlyList<BranchRuleArtifact> branchRules,
        params TaskArtifact[] tasks)
        => new(id, key, key, order, DisabledCondition(), tasks, parallelGroups, branchRules);

    public static TaskArtifact MessagingTask(
        string key,
        int order,
        SemanticVersion version,
        string? topic = null,
        RetryPolicyArtifact? retryPolicy = null,
        TimeoutPolicyArtifact? timeoutPolicy = null,
        OnErrorPolicy onErrorPolicy = OnErrorPolicy.Stop,
        CompensationArtifact? compensation = null,
        TaskExecutionMode executionMode = TaskExecutionMode.Sequential,
        Id? parallelGroupId = null,
        TaskDispatchType dispatchType = TaskDispatchType.FireAndWaitCallback,
        ExecutionConditionArtifact? executionCondition = null,
        TransformationArtifact? transformation = null,
        MessagingTaskConfigurationArtifact? configuration = null)
        => new(
            Id.New(),
            key,
            key,
            order,
            string.Empty,
            TaskKind.Messaging,
            executionMode,
            parallelGroupId,
            executionCondition ?? DisabledCondition(),
            transformation ?? DisabledTransformation(),
            configuration ?? new MessagingTaskConfigurationArtifact(topic ?? key, version, null!),
            retryPolicy,
            timeoutPolicy,
            onErrorPolicy,
            compensation,
            dispatchType,
            true);

    public static CompensationArtifact Compensation(string topic, SemanticVersion version)
        => new(
            TaskKind.Messaging,
            DisabledTransformation(),
            DisabledCondition(),
            new MessagingTaskConfigurationArtifact(topic, version, null!),
            null,
            null,
            TaskDispatchType.FireAndForget);

    public static RetryPolicyArtifact RetryPolicy(int maxRetries, params string[] retryableErrorCodes)
        => new(
            maxRetries,
            RetryStrategyType.Fixed,
            new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
            retryableErrorCodes,
            true);

    public static TimeoutPolicyArtifact TimeoutPolicy(Duration timeout, string errorCode)
        => new(timeout, TimeoutBehavior.Fail, new FailTimeoutBehaviorPolicyArtifact(errorCode));

    public static TimeoutPolicyArtifact ReconcileTimeoutPolicy(Duration timeout, RetryPolicyArtifact retryPolicy)
        => new(
            timeout,
            TimeoutBehavior.Reconcile,
            new ReconcileTimeoutBehaviorPolicyArtifact(OrchestrationActionOnTimeout.Block, retryPolicy));

    public static BranchRuleArtifact BranchRule(
        Id sourceStageId,
        Id targetStageId,
        int order)
        => new(
            Id.New(),
            ElementType.Stage,
            sourceStageId,
            EnabledCondition("true"),
            ElementType.Stage,
            targetStageId);

    public static BranchRuleArtifact BranchRule(
        Id sourceStageId,
        Id targetStageId,
        string expression)
        => new(
            Id.New(),
            ElementType.Stage,
            sourceStageId,
            EnabledCondition(expression),
            ElementType.Stage,
            targetStageId);

    public static ExecutionConditionArtifact Condition(string expression)
        => EnabledCondition(expression);

    public static TransformationArtifact Transformation(string dsl)
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact
        {
            Dsl = dsl
        })
        {
            IsEnabled = true
        };

    public static JsonNode BusinessPayload(params (string Name, object? Value)[] values)
    {
        var payload = new JsonObject();
        foreach (var (name, value) in values)
        {
            payload[name] = ToJsonNode(value);
        }

        return payload;
    }

    private static JsonNode? ToJsonNode(object? value)
        => value switch
        {
            null => null,
            JsonNode jsonNode => jsonNode.DeepClone(),
            bool boolValue => JsonValue.Create(boolValue),
            int intValue => JsonValue.Create(intValue),
            long longValue => JsonValue.Create(longValue),
            decimal decimalValue => JsonValue.Create(decimalValue),
            double doubleValue => JsonValue.Create(doubleValue),
            _ => JsonValue.Create(value.ToString())
        };

    private static ExecutionConditionArtifact DisabledCondition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(string.Empty)))
        {
            IsEnabled = false
        };

    private static ExecutionConditionArtifact EnabledCondition(string expression)
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(expression)))
        {
            IsEnabled = true
        };

    private static TransformationArtifact DisabledTransformation()
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact())
        {
            IsEnabled = false
        };
}
