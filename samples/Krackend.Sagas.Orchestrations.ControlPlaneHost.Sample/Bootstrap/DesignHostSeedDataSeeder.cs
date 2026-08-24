using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

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

    private static readonly Id OwnerTeamId = StableId("01K00000000000000000000040");
    private static readonly Id DomainId = StableId("01K00000000000000000000041");
    private static readonly Id DefinitionId = StableId("01K00000000000000000000042");

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

    public DesignHostSeedDataSeeder(ControlPlaneDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
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

        await _dbContext.SaveChangesAsync(cancellationToken);
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

    private static Id StableId(string value)
        => new(Ulid.Parse(value));
}
