using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Bootstrap;

public sealed class HappyPathOrchestrationSeeder : IHappyPathOrchestrationSeeder
{
    private const string EnvironmentKey = "local";
    private const string OrchestrationKey = "sales.sale.created";
    private const string OrchestrationName = "Sale Created Happy Path";
    private const string ArtifactType = "orchestration-version-snapshot";
    private static readonly HappyPathSeedDefinition[] SeedDefinitions =
    [
        new(
            new SemanticVersion(1, 0, 0),
            StableId("01K00000000000000000000001"),
            StableId("01K00000000000000000000002"),
            StableId("01K00000000000000000000003"),
            StableId("01K00000000000000000000004"),
            StableId("01K00000000000000000000005"),
            StableId("01K00000000000000000000006"),
            StableId("01K00000000000000000000007"),
            StableId("01K00000000000000000000008"),
            "tasks.inventories.reserve.requested",
            "tasks.payments.capture.requested"),
        new(
            new SemanticVersion(1, 1, 0),
            StableId("01K00000000000000000000011"),
            StableId("01K00000000000000000000012"),
            StableId("01K00000000000000000000013"),
            StableId("01K00000000000000000000014"),
            StableId("01K00000000000000000000015"),
            StableId("01K00000000000000000000016"),
            StableId("01K00000000000000000000017"),
            StableId("01K00000000000000000000018"),
            "commands.inventories.stock.reserve",
            "commands.payments.payment.capture.")
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IRuntimeArtifactRepository _repository;

    public HappyPathOrchestrationSeeder(IRuntimeArtifactRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var definition in SeedDefinitions)
        {
            var artifact = BuildArtifact(definition);
            var payload = JsonSerializer.Serialize(artifact, SerializerOptions);
            var checksum = new Checksum(ComputeChecksum(payload));
            var artifactId = await ResolveArtifactIdAsync(definition, cancellationToken);
            var now = DateTime.UtcNow;

            await _repository.Upsert(new RuntimeOrchestrationArtifact
            {
                Id = artifactId,
                EnvironmentKey = EnvironmentKey,
                OrchestrationDefinitionKey = artifact.Key,
                ArtifactType = ArtifactType,
                SourceOrchestrationVersionId = artifact.OrchestrationVersionId,
                Version = artifact.Version,
                ArtifactChecksum = checksum,
                ArtifactPayload = JsonNode.Parse(payload)!,
                IsActive = true,
                LoadedToCache = false,
                DeployedOnUtc = now,
                ActivatedOnUtc = now,
                RetiredOnUtc = null,
                SupersededByArtifactId = null,
                Notes = "Seeded by the runtime host sample for the sales happy path."
            }, cancellationToken);
        }
    }

    private async Task<Id> ResolveArtifactIdAsync(
        HappyPathSeedDefinition definition,
        CancellationToken cancellationToken)
    {
        try
        {
            var current = await _repository.GetByVersion(
                EnvironmentKey,
                OrchestrationKey,
                definition.Version,
                cancellationToken);

            return current.Id;
        }
        catch (KeyNotFoundException)
        {
            return definition.ArtifactId;
        }
    }

    private static OrchestrationArtifact BuildArtifact(HappyPathSeedDefinition definition)
    {
        return new OrchestrationArtifact(
            definition.DefinitionId,
            definition.VersionId,
            OrchestrationKey,
            OrchestrationName,
            "sales",
            definition.Version,
            new Checksum("seeded"),
            [
                new TriggerBindingArtifact(
                    definition.TriggerId,
                    TriggerType.Event,
                    new EventTriggerChannelArtifact(
                        BuildSchemaBinding(
                            definition.TriggerId,
                            ElementType.Orchestration,
                            definition.DefinitionId,
                            definition.RegistryProviderId,
                            "events.sales.sale.created",
                            definition.Version),
                        "events.sales.sale.created",
                        definition.Version),
                    true,
                    "Starts the sales happy path from the sales service event.")
            ],
            [],
            [
                new StageArtifact(
                    definition.StageId,
                    "sale-fulfillment",
                    "Sale fulfillment",
                    1,
                    DisabledCondition(),
                    [
                        BuildMessagingTask(
                            definition.InventoryTaskId,
                            definition.StageId,
                            definition.RegistryProviderId,
                            definition.Version,
                            "inventories.reserve",
                            "Reserve inventory",
                            1,
                            definition.InventoryTopic),
                        BuildMessagingTask(
                            definition.PaymentTaskId,
                            definition.StageId,
                            definition.RegistryProviderId,
                            definition.Version,
                            "payments.capture",
                            "Capture payment",
                            2,
                            definition.PaymentTopic)
                    ],
                    [],
                    [],
                    "Runs the sample inventory and payment services.")
            ],
            "Sample orchestration used to exercise the runtime happy path.",
            definition.Version.ToString(),
            "Registered idempotently by the runtime host.");
    }

    private static TaskArtifact BuildMessagingTask(
        Id id,
        Id stageId,
        Id registryProviderId,
        SemanticVersion version,
        string key,
        string name,
        int order,
        string topic)
    {
        var schemaBinding = BuildSchemaBinding(id, ElementType.Task, stageId, registryProviderId, topic, version);
        var configuration = new MessagingTaskConfigurationArtifact(topic, version, schemaBinding);

        return new TaskArtifact(
            id,
            key,
            name,
            order,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            DisabledCondition(),
            DisabledTransformation(),
            configuration,
            DefaultRetryPolicy(),
            DefaultTimeoutPolicy(),
            OnErrorPolicy.Stop,
            DefaultCompensation(configuration),
            TaskDispatchType.FireAndWaitCallback,
            true);
    }

    private static SchemaBindingArtifact BuildSchemaBinding(
        Id id,
        ElementType elementType,
        Id elementId,
        Id registryProviderId,
        string contractKey,
        SemanticVersion version)
        => new(
            id,
            elementType,
            elementId,
            id,
            contractKey,
            version,
            registryProviderId,
            false)
        {
            IsValidationEnabled = false
        };

    private static ExecutionConditionArtifact DisabledCondition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression("true")))
        {
            IsEnabled = false
        };

    private static TransformationArtifact DisabledTransformation()
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact())
        {
            IsEnabled = false
        };

    private static RetryPolicyArtifact DefaultRetryPolicy()
        => new(
            0,
            RetryStrategyType.Fixed,
            new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
            [],
            true);

    private static TimeoutPolicyArtifact DefaultTimeoutPolicy()
        => new(
            Duration.FromMinutes(5),
            TimeoutBehavior.Fail,
            new FailTimeoutBehaviorPolicyArtifact("TASK_TIMEOUT"));

    private static CompensationArtifact DefaultCompensation(ITaskConfigurationArtifact configuration)
        => new(
            TaskKind.Messaging,
            DisabledTransformation(),
            DisabledCondition(),
            configuration,
            DefaultRetryPolicy(),
            DefaultTimeoutPolicy(),
            TaskDispatchType.FireAndForget);

    private static Id StableId(string value)
        => new(Ulid.Parse(value));

    private static string ComputeChecksum(string payload)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

}
