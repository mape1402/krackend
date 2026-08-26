using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.Extensions.Configuration;

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Bootstrap;

public sealed class HappyPathOrchestrationSeeder : IHappyPathOrchestrationSeeder
{
    private const string EnvironmentKey = "local";
    private const string OrchestrationKey = "sales.sale.created";
    private const string OrchestrationName = "Sale Created Happy Path";
    private const string ArtifactType = "orchestration-version-snapshot";
    private const string ArtifactSchemaVersion = "1.0.0";
    private const string DesignNodeKey = "local-design";
    private const string DesignNodeName = "Local Design Control Plane";
    private const string RemoteRuntimeNodeId = "01K00000000000000000000051";
    private const string DesignInboundClientId = "local-runtime-pull";
    private const string DesignInboundKeyId = "local-design-pull-key";
    private const string DesignInboundSecret = "KrackendLocalDesignInboundSecret_ChangeMe";
    private const string RuntimeInboundClientId = "local-design-push";
    private const string RuntimeInboundKeyId = "local-runtime-push-key";
    private const string RuntimeInboundSecret = "KrackendLocalRuntimeInboundSecret_ChangeMe";
    private const string RuntimeInboundScopes = "artifact:push connection:validate";
    private const string RuntimeOutboundScopes = "release:read artifact:read artifact:ack connection:validate";
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
            "commands.payments.payment.capture."),
        new(
            new SemanticVersion(1, 2, 0),
            StableId("01K00000000000000000000021"),
            StableId("01K00000000000000000000022"),
            StableId("01K00000000000000000000023"),
            StableId("01K00000000000000000000024"),
            StableId("01K00000000000000000000025"),
            StableId("01K00000000000000000000026"),
            StableId("01K00000000000000000000027"),
            StableId("01K00000000000000000000028"),
            "commands.inventories.stock.reserve",
            "commands.payments.payment.capture.",
            StableId("01K00000000000000000000029"),
            StableId("01K00000000000000000000030"),
            StableId("01K00000000000000000000031"),
            "commands.sales.sale.complete")
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Id DesignNodeId = StableId("01K00000000000000000000060");
    private readonly IConfiguration _configuration;
    private readonly IRuntimeDesignNodeRepository _designNodeRepository;
    private readonly IConnectionSecretHasher _secretHasher;
    private readonly IRuntimeDesignNodeSecretProtector _secretProtector;
    private readonly IRuntimeArtifactDeploymentService _deploymentService;

    public HappyPathOrchestrationSeeder(
        IConfiguration configuration,
        IRuntimeDesignNodeRepository designNodeRepository,
        IConnectionSecretHasher secretHasher,
        IRuntimeDesignNodeSecretProtector secretProtector,
        IRuntimeArtifactDeploymentService deploymentService)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _designNodeRepository = designNodeRepository ?? throw new ArgumentNullException(nameof(designNodeRepository));
        _secretHasher = secretHasher ?? throw new ArgumentNullException(nameof(secretHasher));
        _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
        _deploymentService = deploymentService ?? throw new ArgumentNullException(nameof(deploymentService));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await UpsertDesignNodeAsync(DateTime.UtcNow, cancellationToken);

        foreach (var definition in SeedDefinitions)
        {
            var artifact = BuildArtifact(definition);
            var payload = JsonSerializer.Serialize(artifact, SerializerOptions);
            var checksum = ComputeChecksum(payload);
            var now = DateTime.UtcNow;

            await _deploymentService.DeployAsync(new RuntimeArtifactDeliveryPackage
            {
                ReleaseTargetId = $"runtime-sample-seed:{OrchestrationKey}:{artifact.Version}",
                ArtifactId = definition.ArtifactId.ToString(),
                ArtifactType = ArtifactType,
                SchemaVersion = ArtifactSchemaVersion,
                EnvironmentKey = EnvironmentKey,
                OrchestrationDefinitionId = artifact.OrchestrationDefinitionId.ToString(),
                OrchestrationVersionId = artifact.OrchestrationVersionId.ToString(),
                OrchestrationDefinitionKey = artifact.Key,
                Version = artifact.Version.ToString(),
                Checksum = checksum,
                PayloadJson = payload,
                CorrelationId = $"runtime-sample-seed:{artifact.Key}:{artifact.Version}",
                PromotedBy = "runtime-host-sample",
                PromotedOnUtc = now
            }, "runtime-host-sample", cancellationToken);
        }
    }

    private async Task UpsertDesignNodeAsync(DateTime now, CancellationToken cancellationToken)
    {
        var existing = await _designNodeRepository.GetByIdAsync(DesignNodeId, cancellationToken);

        await _designNodeRepository.UpsertAsync(new RuntimeDesignNode
        {
            Id = DesignNodeId,
            Key = DesignNodeKey,
            Name = DesignNodeName,
            EndpointBaseUri = _configuration["SeedData:DesignNode:EndpointBaseUri"] ?? "http://localhost:5085",
            RemoteRuntimeNodeId = RemoteRuntimeNodeId,
            DistributionMode = DistributionConnectionMode.HybridSync,
            AccessTokenTtlSeconds = 86_400,
            TokenRefreshSkewSeconds = 300,
            TokenValidationCacheTtlSeconds = 300,
            InboundClientId = RuntimeInboundClientId,
            InboundKeyId = RuntimeInboundKeyId,
            InboundSecretHash = _secretHasher.HashSecret(RuntimeInboundSecret),
            InboundAllowedScopes = RuntimeInboundScopes,
            InboundCredentialStatus = ConnectionCredentialStatus.Active,
            InboundCredentialCreatedAtUtc = existing?.InboundCredentialCreatedAtUtc ?? now,
            InboundCredentialRotatedAtUtc = now,
            InboundCredentialRevokedAtUtc = null,
            InboundLastFailureReason = string.Empty,
            OutboundClientId = DesignInboundClientId,
            OutboundKeyId = DesignInboundKeyId,
            ProtectedOutboundSecret = _secretProtector.Protect(DesignInboundSecret),
            OutboundRequestedScopes = RuntimeOutboundScopes,
            OutboundCredentialStatus = ConnectionCredentialStatus.Active,
            OutboundCredentialImportedAtUtc = existing?.OutboundCredentialImportedAtUtc ?? now,
            Description = "Local design node seeded for push and manual pull demos.",
            IsEnabled = true,
            CreatedOnUtc = existing?.CreatedOnUtc == default ? now : existing?.CreatedOnUtc ?? now,
            UpdatedOnUtc = now
        }, cancellationToken);
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
            BuildStageDefinitions(definition),
            "Sample orchestration used to exercise the runtime happy path.",
            definition.Version.ToString(),
            "Registered idempotently by the runtime host.");
    }

    private static IReadOnlyList<StageArtifact> BuildStageDefinitions(HappyPathSeedDefinition definition)
    {
        if (definition.CompletionTopic is null ||
            definition.PaymentStageId is null ||
            definition.CompletionStageId is null ||
            definition.CompletionTaskId is null)
        {
            return
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
            ];
        }

        return
        [
            new StageArtifact(
                definition.StageId,
                "inventory-reservation",
                "Inventory reservation",
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
                        definition.InventoryTopic)
                ],
                [],
                [],
                "Reserves inventory for the accepted sale."),
            new StageArtifact(
                definition.PaymentStageId.Value,
                "payment-capture",
                "Payment capture",
                2,
                DisabledCondition(),
                [
                    BuildMessagingTask(
                        definition.PaymentTaskId,
                        definition.PaymentStageId.Value,
                        definition.RegistryProviderId,
                        definition.Version,
                        "payments.capture",
                        "Capture payment",
                        1,
                        definition.PaymentTopic)
                ],
                [],
                [],
                "Captures payment after inventory is reserved."),
            new StageArtifact(
                definition.CompletionStageId.Value,
                "sale-completion",
                "Sale completion",
                3,
                DisabledCondition(),
                [
                    BuildMessagingTask(
                        definition.CompletionTaskId.Value,
                        definition.CompletionStageId.Value,
                        definition.RegistryProviderId,
                        definition.Version,
                        "sales.complete",
                        "Complete sale",
                        1,
                        definition.CompletionTopic)
                ],
                [],
                [],
                "Completes the sale after reservation and payment succeed.")
        ];
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
