using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Bootstrap;

internal sealed class DesignHostSeedDataSeeder : IDesignHostSeedDataSeeder
{
    private const string CreatedBy = "KrackendDemo.DesignHost";
    private const string DomainKey = "sales";
    private const string DomainName = "Sales";
    private const string OrchestrationKey = "sales.sale.created";
    private const string OrchestrationName = "Sale Created Happy Path";
    private const string TriggerTopic = "events.sales.sale.created";
    private const string OwnerTeamKey = "sales-platform";
    private const string OwnerTeamName = "Sales Platform";
    private const string RuntimeNodeCode = "local-runtime";
    private const string DesignInboundClientId = "local-runtime-pull";
    private const string DesignInboundKeyId = "local-design-pull-key";
    private const string DesignInboundSecret = "KrackendLocalDesignInboundSecret_ChangeMe";
    private const string RuntimeInboundClientId = "local-design-push";
    private const string RuntimeInboundKeyId = "local-runtime-push-key";
    private const string RuntimeInboundSecret = "KrackendLocalRuntimeInboundSecret_ChangeMe";
    private const string DesignInboundScopes = "release:read artifact:read artifact:ack connection:validate";
    private const string DesignOutboundScopes = "artifact:push connection:validate";

    private static readonly Id OwnerTeamId = StableId("01K00000000000000000000040");
    private static readonly Id DomainId = StableId("01K00000000000000000000041");
    private static readonly Id DefinitionId = StableId("01K00000000000000000000042");
    private static readonly Id RuntimeNodeId = StableId("01K00000000000000000000051");
    private static readonly Id RuntimeNodePolicyId = StableId("01K00000000000000000000052");

    private static readonly IReadOnlyList<DesignHostSeedDefinition> SeedDefinitions =
    [
        new(
            new SemanticVersion(1, 0, 0),
            StableId("01K00000000000000000000003"),
            StableId("01K00000000000000000000005"),
            StableId("01K00000000000000000000008"),
            [
                new DesignHostSeedStageDefinition(
                    StableId("01K00000000000000000000004"),
                    "sale-fulfillment",
                    "Sale fulfillment",
                    1,
                    "Runs the sample inventory and payment services.",
                    [
                        new DesignHostSeedTaskDefinition(
                            StableId("01K00000000000000000000006"),
                            "inventories.reserve",
                            "Reserve inventory",
                            1,
                            "tasks.inventories.reserve.requested"),
                        new DesignHostSeedTaskDefinition(
                            StableId("01K00000000000000000000007"),
                            "payments.capture",
                            "Capture payment",
                            2,
                            "tasks.payments.capture.requested")
                    ])
            ]),
        new(
            new SemanticVersion(1, 1, 0),
            StableId("01K00000000000000000000013"),
            StableId("01K00000000000000000000015"),
            StableId("01K00000000000000000000018"),
            [
                new DesignHostSeedStageDefinition(
                    StableId("01K00000000000000000000014"),
                    "sale-fulfillment",
                    "Sale fulfillment",
                    1,
                    "Runs the sample inventory and payment services using the command queues.",
                    [
                        new DesignHostSeedTaskDefinition(
                            StableId("01K00000000000000000000016"),
                            "inventories.reserve",
                            "Reserve inventory",
                            1,
                            "commands.inventories.stock.reserve"),
                        new DesignHostSeedTaskDefinition(
                            StableId("01K00000000000000000000017"),
                            "payments.capture",
                            "Capture payment",
                            2,
                            "commands.payments.payment.capture.")
                    ])
            ]),
        new(
            new SemanticVersion(1, 2, 0),
            StableId("01K00000000000000000000023"),
            StableId("01K00000000000000000000025"),
            StableId("01K00000000000000000000028"),
            [
                new DesignHostSeedStageDefinition(
                    StableId("01K00000000000000000000024"),
                    "inventory-reservation",
                    "Inventory reservation",
                    1,
                    "Reserves inventory for the accepted sale.",
                    [
                        new DesignHostSeedTaskDefinition(
                            StableId("01K00000000000000000000026"),
                            "inventories.reserve",
                            "Reserve inventory",
                            1,
                            "commands.inventories.stock.reserve")
                    ]),
                new DesignHostSeedStageDefinition(
                    StableId("01K00000000000000000000029"),
                    "payment-capture",
                    "Payment capture",
                    2,
                    "Captures payment after inventory is reserved.",
                    [
                        new DesignHostSeedTaskDefinition(
                            StableId("01K00000000000000000000027"),
                            "payments.capture",
                            "Capture payment",
                            1,
                            "commands.payments.payment.capture.")
                    ]),
                new DesignHostSeedStageDefinition(
                    StableId("01K00000000000000000000030"),
                    "sale-completion",
                    "Sale completion",
                    3,
                    "Completes the sale after reservation and payment succeed.",
                    [
                        new DesignHostSeedTaskDefinition(
                            StableId("01K00000000000000000000031"),
                            "sales.complete",
                            "Complete sale",
                            1,
                            "commands.sales.sale.complete")
                    ])
            ])
    ];

    private readonly ControlPlaneDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IOrchestrationVersionRepository _versionRepository;
    private readonly IOrchestrationDefinitionRepository _definitionRepository;
    private readonly IOrchestrationVersionArtifactSnapshotBuilder _artifactSnapshotBuilder;
    private readonly IOrchestrationArtifactPayloadFactory _artifactPayloadFactory;
    private readonly IArtifactPublicationApplicationService _artifactPublicationService;
    private readonly IConnectionSecretHasher _secretHasher;
    private readonly IControlPlaneRuntimeNodeSecretProtector _secretProtector;

    public DesignHostSeedDataSeeder(
        ControlPlaneDbContext dbContext,
        IConfiguration configuration,
        IOrchestrationVersionRepository versionRepository,
        IOrchestrationDefinitionRepository definitionRepository,
        IOrchestrationVersionArtifactSnapshotBuilder artifactSnapshotBuilder,
        IOrchestrationArtifactPayloadFactory artifactPayloadFactory,
        IArtifactPublicationApplicationService artifactPublicationService,
        IConnectionSecretHasher secretHasher,
        IControlPlaneRuntimeNodeSecretProtector secretProtector)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _artifactSnapshotBuilder = artifactSnapshotBuilder ?? throw new ArgumentNullException(nameof(artifactSnapshotBuilder));
        _artifactPayloadFactory = artifactPayloadFactory ?? throw new ArgumentNullException(nameof(artifactPayloadFactory));
        _artifactPublicationService = artifactPublicationService ?? throw new ArgumentNullException(nameof(artifactPublicationService));
        _secretHasher = secretHasher ?? throw new ArgumentNullException(nameof(secretHasher));
        _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        await UpsertSecurityTeamAsync(now, cancellationToken);
        await UpsertDomainAsync(now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var definition = await UpsertOrchestrationDefinitionAsync(now, cancellationToken);

        foreach (var seedDefinition in SeedDefinitions)
        {
            await UpsertOrchestrationVersionAsync(definition.Id, seedDefinition, now, cancellationToken);
        }

        await UpsertDistributionAsync(now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var seedDefinition in SeedDefinitions)
        {
            await EnsureArtifactPublishedAsync(seedDefinition.VersionId, now, cancellationToken);
        }
    }

    private async Task UpsertSecurityTeamAsync(DateTime now, CancellationToken cancellationToken)
    {
        var team = await _dbContext.Teams.FirstOrDefaultAsync(x => x.Key == OwnerTeamKey, cancellationToken);

        if (team is null)
        {
            _dbContext.Teams.Add(new TeamEntity
            {
                Id = OwnerTeamId,
                Key = OwnerTeamKey,
                DisplayName = OwnerTeamName,
                Description = "Owns the sales demo orchestrations.",
                IsActive = true,
                CreatedOnUtc = now
            });

            return;
        }

        team.DisplayName = OwnerTeamName;
        team.Description = "Owns the sales demo orchestrations.";
        team.IsActive = true;
        team.UpdatedOnUtc = now;
    }

    private async Task UpsertDomainAsync(DateTime now, CancellationToken cancellationToken)
    {
        var domain = await _dbContext.Domains.FirstOrDefaultAsync(x => x.Key == DomainKey, cancellationToken);

        if (domain is null)
        {
            _dbContext.Domains.Add(new DomainEntity
            {
                Id = DomainId,
                Key = DomainKey,
                DisplayName = DomainName,
                Description = "Sales demo bounded context.",
                IsActive = true,
                CreatedOnUtc = now
            });

            return;
        }

        domain.DisplayName = DomainName;
        domain.Description = "Sales demo bounded context.";
        domain.IsActive = true;
        domain.UpdatedOnUtc = now;
    }

    private async Task<OrchestrationDefinitionEntity> UpsertOrchestrationDefinitionAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var definition = await _dbContext.OrchestrationDefinitions.FirstOrDefaultAsync(
            x => x.Key == OrchestrationKey,
            cancellationToken);

        if (definition is null)
        {
            definition = new OrchestrationDefinitionEntity
            {
                Id = DefinitionId,
                Key = OrchestrationKey,
                Name = OrchestrationName,
                Domain = DomainKey,
                DomainId = DomainId,
                IsActive = true,
                CreatedOnUtc = now,
                CreatedBy = CreatedBy,
                Description = "Sample orchestration used to exercise the runtime happy path.",
                OwnerTeam = OwnerTeamKey,
                OwnerTeamId = OwnerTeamId,
                Tags = ["demo", "sales", "happy-path"]
            };

            _dbContext.OrchestrationDefinitions.Add(definition);
            return definition;
        }

        definition.Name = OrchestrationName;
        definition.Domain = DomainKey;
        definition.DomainId = DomainId;
        definition.IsActive = true;
        definition.UpdatedOnUtc = now;
        definition.UpdatedBy = CreatedBy;
        definition.Description = "Sample orchestration used to exercise the runtime happy path.";
        definition.OwnerTeam = OwnerTeamKey;
        definition.OwnerTeamId = OwnerTeamId;
        definition.Tags = ["demo", "sales", "happy-path"];

        return definition;
    }

    private async Task UpsertOrchestrationVersionAsync(
        Id definitionId,
        DesignHostSeedDefinition seedDefinition,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var version = seedDefinition.Version.ToString();
        var versionEntity = await _dbContext.OrchestrationVersions.FirstOrDefaultAsync(
            x => x.OrchestrationDefinitionId == definitionId && x.Version == version,
            cancellationToken);

        if (versionEntity is null)
        {
            versionEntity = new OrchestrationVersionEntity
            {
                Id = seedDefinition.VersionId,
                OrchestrationDefinitionId = definitionId,
                Version = version,
                Status = OrchestrationVersionStatus.Deployed,
                VersionLabel = version,
                Description = $"Runtime demo artifact for {OrchestrationKey} v{version}.",
                Checksum = $"seeded-{OrchestrationKey}-{version}",
                Notes = "Registered idempotently by the control plane host sample.",
                CreatedOnUtc = now,
                CreatedBy = CreatedBy,
                ApprovedOnUtc = now,
                ApprovedBy = CreatedBy
            };

            _dbContext.OrchestrationVersions.Add(versionEntity);
        }
        else
        {
            versionEntity.Status = OrchestrationVersionStatus.Deployed;
            versionEntity.VersionLabel = version;
            versionEntity.Description = $"Runtime demo artifact for {OrchestrationKey} v{version}.";
            versionEntity.Checksum = $"seeded-{OrchestrationKey}-{version}";
            versionEntity.Notes = "Registered idempotently by the control plane host sample.";
            versionEntity.ApprovedOnUtc ??= now;
            versionEntity.ApprovedBy ??= CreatedBy;
            versionEntity.UpdatedOnUtc = now;
            versionEntity.UpdatedBy = CreatedBy;
        }

        await UpsertTriggerAsync(definitionId, versionEntity.Id, seedDefinition, cancellationToken);

        foreach (var stage in seedDefinition.Stages)
        {
            await UpsertStageAsync(versionEntity.Id, seedDefinition, stage, cancellationToken);
        }
    }

    private async Task UpsertTriggerAsync(
        Id definitionId,
        Id versionId,
        DesignHostSeedDefinition seedDefinition,
        CancellationToken cancellationToken)
    {
        var trigger = await _dbContext.TriggerBindings.FirstOrDefaultAsync(
            x => x.OrchestrationVersionId == versionId && x.Key == "sale-created",
            cancellationToken);

        var triggerChannel = new TriggerChannelEnvelopeJsonModel
        {
            Type = "event",
            Event = new EventTriggerChannelJsonModel
            {
                Topic = TriggerTopic,
                Version = seedDefinition.Version.ToString(),
                SchemaBinding = CreateSchemaBinding(
                    seedDefinition.TriggerId,
                    ElementType.Orchestration,
                    definitionId,
                    seedDefinition.RegistryProviderId,
                    TriggerTopic,
                    seedDefinition.Version)
            }
        };

        if (trigger is null)
        {
            _dbContext.TriggerBindings.Add(new TriggerBindingEntity
            {
                Id = seedDefinition.TriggerId,
                OrchestrationVersionId = versionId,
                Key = "sale-created",
                TriggerType = TriggerType.Event,
                IsEnabled = true,
                Description = "Starts the sales happy path from the sales service event.",
                TriggerChannel = triggerChannel
            });

            return;
        }

        trigger.TriggerType = TriggerType.Event;
        trigger.IsEnabled = true;
        trigger.Description = "Starts the sales happy path from the sales service event.";
        trigger.TriggerChannel = triggerChannel;
    }

    private async Task UpsertStageAsync(
        Id versionId,
        DesignHostSeedDefinition seedDefinition,
        DesignHostSeedStageDefinition seedStage,
        CancellationToken cancellationToken)
    {
        var stage = await _dbContext.StageDefinitions.FirstOrDefaultAsync(
            x => x.OrchestrationVersionId == versionId && x.Key == seedStage.Key,
            cancellationToken);

        if (stage is null)
        {
            stage = new StageDefinitionEntity
            {
                Id = seedStage.Id,
                OrchestrationVersionId = versionId,
                Key = seedStage.Key,
                Name = seedStage.Name,
                Order = seedStage.Order,
                Description = seedStage.Description,
                ExecutionCondition = DisabledCondition()
            };

            _dbContext.StageDefinitions.Add(stage);
        }
        else
        {
            stage.Name = seedStage.Name;
            stage.Order = seedStage.Order;
            stage.Description = seedStage.Description;
            stage.ExecutionCondition = DisabledCondition();
        }

        foreach (var task in seedStage.Tasks)
        {
            await UpsertTaskAsync(stage.Id, seedDefinition, task, cancellationToken);
        }
    }

    private async Task UpsertTaskAsync(
        Id stageId,
        DesignHostSeedDefinition seedDefinition,
        DesignHostSeedTaskDefinition seedTask,
        CancellationToken cancellationToken)
    {
        var task = await _dbContext.TaskDefinitions.FirstOrDefaultAsync(
            x => x.StageDefinitionId == stageId && x.Key == seedTask.Key,
            cancellationToken);

        var configuration = CreateMessagingConfiguration(
            seedTask.Id,
            ElementType.Task,
            stageId,
            seedDefinition.RegistryProviderId,
            seedTask.Topic,
            seedDefinition.Version);

        if (task is null)
        {
            _dbContext.TaskDefinitions.Add(new TaskDefinitionEntity
            {
                Id = seedTask.Id,
                StageDefinitionId = stageId,
                Key = seedTask.Key,
                Name = seedTask.Name,
                Order = seedTask.Order,
                Kind = TaskKind.Messaging,
                ExecutionMode = TaskExecutionMode.Sequential,
                ParallelGroupId = null,
                OnErrorPolicy = OnErrorPolicy.Stop,
                DispatchType = TaskDispatchType.FireAndWaitCallback,
                IsEnabled = true,
                Notes = "Seeded from the runtime happy path.",
                ExecutionCondition = DisabledCondition(),
                Transformation = DisabledTransformation(),
                Configuration = configuration,
                RetryPolicy = DefaultRetryPolicy(),
                TimeoutPolicy = DefaultTimeoutPolicy(),
                CompensationDefinition = DefaultCompensation(
                    seedTask.Id,
                    stageId,
                    seedDefinition.RegistryProviderId,
                    seedTask.Topic,
                    seedDefinition.Version)
            });

            return;
        }

        task.Name = seedTask.Name;
        task.Order = seedTask.Order;
        task.Kind = TaskKind.Messaging;
        task.ExecutionMode = TaskExecutionMode.Sequential;
        task.ParallelGroupId = null;
        task.OnErrorPolicy = OnErrorPolicy.Stop;
        task.DispatchType = TaskDispatchType.FireAndWaitCallback;
        task.IsEnabled = true;
        task.Notes = "Seeded from the runtime happy path.";
        task.ExecutionCondition = DisabledCondition();
        task.Transformation = DisabledTransformation();
        task.Configuration = configuration;
        task.RetryPolicy = DefaultRetryPolicy();
        task.TimeoutPolicy = DefaultTimeoutPolicy();
        task.CompensationDefinition = DefaultCompensation(
            seedTask.Id,
            stageId,
            seedDefinition.RegistryProviderId,
            seedTask.Topic,
            seedDefinition.Version);
    }

    private static TaskConfigurationEnvelopeJsonModel CreateMessagingConfiguration(
        Id id,
        ElementType elementType,
        Id elementId,
        Id registryProviderId,
        string topic,
        SemanticVersion version)
        => new()
        {
            Type = "messaging",
            Messaging = new MessagingTaskConfigurationJsonModel
            {
                Topic = topic,
                Version = version.ToString(),
                SchemaBinding = CreateSchemaBinding(id, elementType, elementId, registryProviderId, topic, version)
            }
        };

    private static SchemaBindingJsonModel CreateSchemaBinding(
        Id id,
        ElementType elementType,
        Id elementId,
        Id registryProviderId,
        string contractKey,
        SemanticVersion version)
        => new()
        {
            Id = id.ToString(),
            ElementType = elementType,
            ElementId = elementId.ToString(),
            ContractId = id.ToString(),
            ContractKey = contractKey,
            ContractVersion = version.ToString(),
            RegistryProviderId = registryProviderId.ToString(),
            StrictMode = false,
            IsValidationEnabled = false
        };

    private static ExecutionConditionJsonModel DisabledCondition()
        => new()
        {
            IsEnabled = false,
            Engine = EngineType.DSL,
            Configuration = new ConditionConfigurationEnvelopeJsonModel
            {
                Type = "dsl",
                Dsl = new DslConditionConfigurationJsonModel { Expression = "true" }
            }
        };

    private static TransformationDefinitionJsonModel DisabledTransformation()
        => new()
        {
            IsEnabled = false,
            Engine = EngineType.DSL,
            Configuration = new TransformationConfigurationEnvelopeJsonModel
            {
                Type = "dsl",
                Dsl = new DslTransformationConfigurationJsonModel()
            }
        };

    private static RetryPolicyJsonModel DefaultRetryPolicy()
        => new()
        {
            MaxRetries = 0,
            StrategyType = RetryStrategyType.Fixed,
            Strategy = new RetryStrategyEnvelopeJsonModel
            {
                Type = "fixed",
                Fixed = new FixedRetryStrategyJsonModel { Delay = TimeSpan.FromSeconds(1) }
            },
            RetryableErrorCodes = [],
            StopOnNonRetryableError = true
        };

    private static TimeoutPolicyJsonModel DefaultTimeoutPolicy()
        => new()
        {
            Timeout = TimeSpan.FromMinutes(5),
            TimeoutBehavior = TimeoutBehavior.Fail,
            TimeoutBehaviorPolicy = new TimeoutBehaviorPolicyEnvelopeJsonModel
            {
                Type = "fail",
                Fail = new FailTimeoutBehaviorPolicyJsonModel { ErrorCode = "TASK_TIMEOUT" }
            }
        };

    private static CompensationDefinitionJsonModel DefaultCompensation(
        Id taskId,
        Id stageId,
        Id registryProviderId,
        string topic,
        SemanticVersion version)
        => new()
        {
            HasExecutionCondition = false,
            HasTransformation = false,
            CompensationTaskKind = TaskKind.Messaging,
            Transformation = DisabledTransformation(),
            ExecutionCondition = DisabledCondition(),
            Configuration = CreateMessagingConfiguration(taskId, ElementType.Task, stageId, registryProviderId, topic, version),
            RetryPolicy = DefaultRetryPolicy(),
            TimeoutPolicy = DefaultTimeoutPolicy(),
            DispatchType = TaskDispatchType.FireAndForget
        };

    private async Task UpsertDistributionAsync(DateTime now, CancellationToken cancellationToken)
    {
        var runtimeNode = await _dbContext.RuntimeNodes.FirstOrDefaultAsync(x => x.Code == RuntimeNodeCode, cancellationToken);
        if (runtimeNode is null)
        {
            _dbContext.RuntimeNodes.Add(new RuntimeNodeEntity
            {
                Id = RuntimeNodeId,
                Name = "Local Runtime",
                Code = RuntimeNodeCode,
                DistributionMode = DistributionMode.HybridSync,
                EndpointBaseUri = _configuration["SeedData:RuntimeNode:EndpointBaseUri"] ?? "http://localhost:5227",
                EndpointApiPath = "runtime/artifacts/deploy",
                Status = RuntimeNodeStatus.Enabled,
                IsEnabled = true,
                IsDeleted = false,
                DeletedAtUtc = null,
                Description = "Local runtime node seeded for push and manual pull demos.",
                AccessTokenTtlSeconds = 86_400,
                TokenRefreshSkewSeconds = 300,
                TokenValidationCacheTtlSeconds = 300,
                InboundClientId = DesignInboundClientId,
                InboundKeyId = DesignInboundKeyId,
                InboundSecretHash = _secretHasher.HashSecret(DesignInboundSecret),
                InboundAllowedScopes = DesignInboundScopes,
                InboundCredentialStatus = ConnectionCredentialStatus.Active,
                InboundCredentialCreatedAtUtc = now,
                InboundCredentialRotatedAtUtc = now,
                InboundLastFailureReason = string.Empty,
                OutboundClientId = RuntimeInboundClientId,
                OutboundKeyId = RuntimeInboundKeyId,
                ProtectedOutboundSecret = _secretProtector.Protect(RuntimeInboundSecret),
                OutboundRequestedScopes = DesignOutboundScopes,
                OutboundCredentialStatus = ConnectionCredentialStatus.Active,
                OutboundCredentialImportedAtUtc = now,
                RegisteredAtUtc = now
            });
        }
        else
        {
            runtimeNode.Name = "Local Runtime";
            runtimeNode.DistributionMode = DistributionMode.HybridSync;
            runtimeNode.EndpointBaseUri = _configuration["SeedData:RuntimeNode:EndpointBaseUri"] ?? "http://localhost:5227";
            runtimeNode.EndpointApiPath = "runtime/artifacts/deploy";
            runtimeNode.Status = RuntimeNodeStatus.Enabled;
            runtimeNode.IsEnabled = true;
            runtimeNode.IsDeleted = false;
            runtimeNode.DeletedAtUtc = null;
            runtimeNode.Description = "Local runtime node seeded for push and manual pull demos.";
            runtimeNode.AccessTokenTtlSeconds = 86_400;
            runtimeNode.TokenRefreshSkewSeconds = 300;
            runtimeNode.TokenValidationCacheTtlSeconds = 300;
            runtimeNode.InboundClientId = DesignInboundClientId;
            runtimeNode.InboundKeyId = DesignInboundKeyId;
            runtimeNode.InboundSecretHash = _secretHasher.HashSecret(DesignInboundSecret);
            runtimeNode.InboundAllowedScopes = DesignInboundScopes;
            runtimeNode.InboundCredentialStatus = ConnectionCredentialStatus.Active;
            runtimeNode.InboundCredentialCreatedAtUtc ??= now;
            runtimeNode.InboundCredentialRotatedAtUtc = now;
            runtimeNode.InboundCredentialRevokedAtUtc = null;
            runtimeNode.InboundLastFailureReason = string.Empty;
            runtimeNode.OutboundClientId = RuntimeInboundClientId;
            runtimeNode.OutboundKeyId = RuntimeInboundKeyId;
            runtimeNode.ProtectedOutboundSecret = _secretProtector.Protect(RuntimeInboundSecret);
            runtimeNode.OutboundRequestedScopes = DesignOutboundScopes;
            runtimeNode.OutboundCredentialStatus = ConnectionCredentialStatus.Active;
            runtimeNode.OutboundCredentialImportedAtUtc ??= now;
            runtimeNode.LastUpdatedAtUtc = now;
        }

        var policy = await _dbContext.OrchestrationAllowedRuntimeNodes.FirstOrDefaultAsync(
            x => x.OrchestrationDefinitionId == DefinitionId.ToString() && x.RuntimeNodeId == RuntimeNodeId,
            cancellationToken);
        if (policy is null)
        {
            _dbContext.OrchestrationAllowedRuntimeNodes.Add(new OrchestrationAllowedRuntimeNodeEntity
            {
                Id = RuntimeNodePolicyId,
                OrchestrationDefinitionId = DefinitionId.ToString(),
                RuntimeNodeId = RuntimeNodeId,
                CreatedAtUtc = now,
                CreatedBy = CreatedBy
            });
        }
    }

    private async Task EnsureArtifactPublishedAsync(
        Id versionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var version = await _versionRepository.GetById(versionId, cancellationToken);
        var definition = await _definitionRepository.GetById(version.OrchestrationDefinitionId, cancellationToken);
        var snapshot = await _artifactSnapshotBuilder.Build(version, cancellationToken);
        var payloadJson = _artifactPayloadFactory.CreatePayloadJson(definition, snapshot);

        await _artifactPublicationService.PublishDeployment(new OrchestrationVersionDeployedEvent(
            version.Id.ToString(),
            version.OrchestrationDefinitionId.ToString(),
            definition.Name,
            version.VersionLabel,
            version.Version.ToString(),
            payloadJson,
            version.Checksum.Value,
            CreatedBy,
            version.Id.ToString(),
            now), cancellationToken);
    }

    private static Id StableId(string value)
        => new(Ulid.Parse(value));
}
