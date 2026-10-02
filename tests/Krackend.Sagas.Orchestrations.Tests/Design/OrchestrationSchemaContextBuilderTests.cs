using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using NSubstitute;
using ControlPlaneSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class OrchestrationSchemaContextBuilderTests
{
    private const string TriggerMetadataAlias = "trigger_metadata";

    [Fact]
    public async Task BuildForTask_ExposesOnlyTriggerForFirstTask()
    {
        var version = CreateVersion();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, version.StageDefinitions[0].TaskDefinitions[0].Id);

        var source = Assert.Single(context.Sources, x => x.Alias == "trigger");
        Assert.Equal("trigger", source.Alias);
        Assert.Equal(SchemaContractKind.Event, source.SchemaBinding.ContractKind);
        var triggerMetadata = Assert.Single(context.Sources, x => x.Alias == TriggerMetadataAlias);
        Assert.Equal(OrchestrationSchemaContextSourceKind.TriggerMetadata, triggerMetadata.SourceKind);
        Assert.Equal(OrchestrationMetadataConstants.TriggerMetadataKey, triggerMetadata.SchemaBinding.ContractKey);
        Assert.NotNull(triggerMetadata.SchemaBinding.Snapshot);
        Assert.Equal("reserve_inventory", context.Target.Alias);
        Assert.Equal(SchemaContractKind.CommandRequest, context.Target.SchemaBinding.ContractKind);
        Assert.False(string.IsNullOrWhiteSpace(context.Signature));
    }

    [Fact]
    public async Task BuildForTask_ExposesMetadataDescriptorsAsSeparateSources()
    {
        var version = CreateVersion();
        var repository = Substitute.For<IMetadataDescriptorRepository>();
        var audit = MetadataDescriptor("audit_metadata", "AuditMetadata");
        var security = MetadataDescriptor("security_metadata", "SecurityMetadata");
        repository.GetAllDescriptors(Arg.Any<CancellationToken>()).Returns([audit, security]);
        var builder = new OrchestrationSchemaContextBuilder(repository);

        var context = await builder.BuildForTask(version, version.StageDefinitions[0].TaskDefinitions[0].Id);

        var auditSource = Assert.Single(context.Sources, x => x.Alias == "audit_metadata");
        Assert.Equal(OrchestrationSchemaContextSourceKind.Metadata, auditSource.SourceKind);
        Assert.Equal("audit_metadata", auditSource.SchemaBinding.ContractKey);
        Assert.Equal("JsonSchema", auditSource.SchemaBinding.Snapshot!.SchemaFormat);
        Assert.Equal(audit.SchemaJson, auditSource.SchemaBinding.Snapshot.SchemaJson);
        Assert.Equal(audit.ContentHash, auditSource.SchemaBinding.Snapshot.ContentHash);

        var securitySource = Assert.Single(context.Sources, x => x.Alias == "security_metadata");
        Assert.Equal(OrchestrationSchemaContextSourceKind.Metadata, securitySource.SourceKind);
        Assert.Equal("security_metadata", securitySource.SchemaBinding.ContractKey);
        Assert.Equal(security.SchemaJson, securitySource.SchemaBinding.Snapshot!.SchemaJson);
        Assert.Equal(security.ContentHash, securitySource.SchemaBinding.Snapshot.ContentHash);

        Assert.DoesNotContain(context.Sources, x => x.Alias == "metadata");
        Assert.Contains(context.Sources, x => x.Alias == "trigger");
        Assert.Contains(context.Sources, x => x.Alias == TriggerMetadataAlias);
        Assert.False(string.IsNullOrWhiteSpace(context.Signature));
    }

    [Fact]
    public async Task BuildForTask_ExposesPreviousSequentialTaskRequestsAndResponses()
    {
        var version = CreateVersion();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, version.StageDefinitions[0].TaskDefinitions[1].Id);

        Assert.Contains(context.Sources, x => x.Alias == "trigger");
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory");
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory_reply");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "authorize_payment");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "authorize_payment_reply");
    }

    [Fact]
    public async Task BuildForTask_DoesNotExposeParallelSiblingRequestsOrResponses()
    {
        var version = CreateVersion();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, version.StageDefinitions[0].TaskDefinitions[2].Id);

        Assert.Contains(context.Sources, x => x.Alias == "trigger");
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory");
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory_reply");
        Assert.Contains(context.Sources, x => x.Alias == "authorize_payment");
        Assert.Contains(context.Sources, x => x.Alias == "authorize_payment_reply");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "notify_customer");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "notify_customer_reply");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "audit_sale");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "audit_sale_reply");
    }

    [Fact]
    public async Task BuildForTask_ExposesPreviousStageAndJoinedParallelRequestsAndResponses()
    {
        var version = CreateVersion();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, version.StageDefinitions[1].TaskDefinitions[0].Id);

        Assert.Contains(context.Sources, x => x.Alias == "trigger");
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory");
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory_reply");
        Assert.Contains(context.Sources, x => x.Alias == "authorize_payment");
        Assert.Contains(context.Sources, x => x.Alias == "authorize_payment_reply");
        Assert.Contains(context.Sources, x => x.Alias == "notify_customer");
        Assert.Contains(context.Sources, x => x.Alias == "notify_customer_reply");
        Assert.Contains(context.Sources, x => x.Alias == "audit_sale");
        Assert.Contains(context.Sources, x => x.Alias == "audit_sale_reply");
        Assert.Equal("close_sale", context.Target.Alias);
    }

    [Fact]
    public async Task BuildForTask_UsesDslSafeAliases()
    {
        var version = CreateVersionWithRepeatedTaskKey();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, version.StageDefinitions[2].TaskDefinitions[0].Id);

        Assert.Contains(context.Sources, x => x.Alias == "audit");
        Assert.Contains(context.Sources, x => x.Alias == "audit_reply");
        Assert.Contains(context.Sources, x => x.Alias == "enrichment_audit");
        Assert.Contains(context.Sources, x => x.Alias == "enrichment_audit_reply");
        Assert.Equal("close_sale", context.Target.Alias);
        Assert.All(context.Sources.Append(new OrchestrationSchemaSource { Alias = context.Target.Alias }), source =>
        {
            Assert.DoesNotContain(".", source.Alias, StringComparison.Ordinal);
            Assert.DoesNotContain("-", source.Alias, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task BuildForStage_ExposesTriggerAndPreviousStageTaskRequestsAndResponses()
    {
        var version = CreateVersion();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForStage(version, version.StageDefinitions[1].Id);

        Assert.Null(context.Target);
        Assert.Contains(context.Sources, x => x.Alias == "trigger");
        Assert.Contains(context.Sources, x => x.Alias == TriggerMetadataAlias);
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory");
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory_reply");
        Assert.Contains(context.Sources, x => x.Alias == "authorize_payment");
        Assert.Contains(context.Sources, x => x.Alias == "authorize_payment_reply");
        Assert.Contains(context.Sources, x => x.Alias == "notify_customer");
        Assert.Contains(context.Sources, x => x.Alias == "notify_customer_reply");
        Assert.Contains(context.Sources, x => x.Alias == "audit_sale");
        Assert.Contains(context.Sources, x => x.Alias == "audit_sale_reply");
        Assert.False(string.IsNullOrWhiteSpace(context.Signature));
    }

    [Fact]
    public async Task BuildForTask_ThrowsWhenTaskDoesNotExist()
    {
        var version = CreateVersion();
        var builder = new OrchestrationSchemaContextBuilder();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.BuildForTask(version, Id.New()));

        Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildForTask_AllowsMissingTriggerAndMissingBindings()
    {
        var version = CreateVersion();
        version.TriggerBindings.Clear();
        version.StageDefinitions[0].TaskDefinitions[0].Configuration = new PluginTaskConfiguration { PluginId = Id.New() };
        var target = version.StageDefinitions[0].TaskDefinitions[1];
        var targetConfiguration = (MessagingTaskConfiguration)target.Configuration;
        targetConfiguration.RequestSchemaBinding = null;
        targetConfiguration.SchemaBinding = null;

        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, target.Id);

        var source = Assert.Single(context.Sources);
        Assert.Equal(TriggerMetadataAlias, source.Alias);
        Assert.Equal(OrchestrationSchemaContextSourceKind.TriggerMetadata, source.SourceKind);
        Assert.Equal(OrchestrationMetadataConstants.TriggerMetadataKey, source.SchemaBinding.ContractKey);
        Assert.Null(context.Target.SchemaBinding);
        Assert.False(string.IsNullOrWhiteSpace(context.Signature));
    }

    [Fact]
    public async Task BuildForTask_UsesSchemaBindingSnapshotWhenRequestBindingHasNoSnapshot()
    {
        var version = CreateVersion();
        var target = version.StageDefinitions[0].TaskDefinitions[0];
        var messaging = (MessagingTaskConfiguration)target.Configuration;
        messaging.SchemaBinding = Binding("commands.reserve_inventory", SchemaContractKind.CommandRequest);
        messaging.RequestSchemaBinding = BindingWithoutSnapshot("commands.reserve_inventory", SchemaContractKind.CommandRequest);

        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, target.Id);

        Assert.Equal("commands.reserve_inventory.hash", context.Target.SchemaBinding.Snapshot!.ContentHash);
    }

    [Fact]
    public async Task BuildForTask_DoesNotExposeEmptyResponseBindingsAsSources()
    {
        var version = CreateVersion();
        var firstTask = version.StageDefinitions[0].TaskDefinitions[0];
        var firstTaskMessaging = (MessagingTaskConfiguration)firstTask.Configuration;
        firstTaskMessaging.ResponseSchemaBinding = new SchemaBinding
        {
            Id = Id.New(),
            ElementType = ElementType.Task,
            ElementId = firstTask.Id,
            ContractId = Id.New(),
            ContractKey = string.Empty,
            ContractVersion = new SemanticVersion(0, 0, 0),
            RegistryProviderId = Id.New(),
            ContractKind = SchemaContractKind.CommandResponse
        };

        var target = version.StageDefinitions[0].TaskDefinitions[1];
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, target.Id);

        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "reserve_inventory_reply");
    }

    [Fact]
    public async Task BuildForTask_DoesNotExposeResponseBindingReferencesWithoutSnapshotsAsSources()
    {
        var version = CreateVersion();
        var firstTask = version.StageDefinitions[0].TaskDefinitions[0];
        var firstTaskMessaging = (MessagingTaskConfiguration)firstTask.Configuration;
        firstTaskMessaging.ResponseSchemaBinding = BindingWithoutSnapshot(
            "commands.inventories.stock.discount",
            SchemaContractKind.CommandResponse);

        var target = version.StageDefinitions[0].TaskDefinitions[1];
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, target.Id);

        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "reserve_inventory_reply");
    }

    [Fact]
    public async Task BuildForTaskCompensation_ExposesForwardSourcesThroughOwningTask()
    {
        var version = CreateVersion();
        var target = version.StageDefinitions[0].TaskDefinitions[1];
        target.CompensationDefinition = Compensation("authorize-payment.undo");
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTaskCompensation(version, target.Id);

        Assert.Contains(context.Sources, x => x.Alias == "trigger");
        Assert.Contains(context.Sources, x => x.Alias == TriggerMetadataAlias);
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory");
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory_reply");
        Assert.Contains(context.Sources, x => x.Alias == "authorize_payment");
        Assert.Contains(context.Sources, x => x.Alias == "authorize_payment_reply");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "notify_customer");
        Assert.Equal("authorize_payment_compensation_request", context.Target.Alias);
        Assert.Equal("authorize-payment.undo.request", context.Target.SchemaBinding.ContractKey);
    }

    [Fact]
    public async Task BuildForTriggerCompensation_ExposesTriggerMetadataAndAllForwardTaskSources()
    {
        var version = CreateVersion();
        var trigger = version.TriggerBindings.Single();
        trigger.CompensationDefinition = Compensation("sale-created.undo");
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTriggerCompensation(version, trigger.Id);

        Assert.Contains(context.Sources, x => x.Alias == "trigger");
        Assert.Contains(context.Sources, x => x.Alias == TriggerMetadataAlias);
        Assert.Contains(context.Sources, x => x.Alias == "reserve_inventory");
        Assert.Contains(context.Sources, x => x.Alias == "audit_sale_reply");
        Assert.Contains(context.Sources, x => x.Alias == "close_sale");
        Assert.Contains(context.Sources, x => x.Alias == "close_sale_reply");
        Assert.Equal("sale_created_compensation_request", context.Target.Alias);
        Assert.Equal("sale-created.undo.request", context.Target.SchemaBinding.ContractKey);
    }

    private static OrchestrationVersion CreateVersion()
    {
        var versionId = Id.New();
        var stageOneId = Id.New();
        var stageTwoId = Id.New();
        var parallelGroupId = Id.New();

        return new OrchestrationVersion
        {
            Id = versionId,
            OrchestrationDefinitionId = Id.New(),
            Version = new SemanticVersion(1, 2, 0),
            Status = OrchestrationVersionStatus.Draft,
            Checksum = new Checksum("schema-context-fixture"),
            CreatedBy = "tests",
            CreatedOnUtc = DateTime.UtcNow,
            TriggerBindings =
            [
                new TriggerBinding
                {
                    Id = Id.New(),
                    OrchestrationVersionId = versionId,
                    Key = "sale-created",
                    TriggerType = TriggerType.Event,
                    IsEnabled = true,
                    TriggerChannel = new EventTriggerChannel
                    {
                        Topic = "events.sales.sale.created",
                        Version = new SemanticVersion(1, 2, 0),
                        HasSchemaValidation = true,
                        SchemaBinding = Binding("sales.sale.created", SchemaContractKind.Event)
                    }
                }
            ],
            StageDefinitions =
            [
                new StageDefinition
                {
                    Id = stageOneId,
                    OrchestrationVersionId = versionId,
                    Key = "intake",
                    Name = "Intake",
                    Order = 1,
                    ParallelGroups =
                    [
                        new ParallelGroupDefinition
                        {
                            Id = parallelGroupId,
                            StageDefinitionId = stageOneId,
                            Name = "post-sale",
                            JoinPolicy = ParallelJoinPolicy.WaitAll
                        }
                    ],
                    TaskDefinitions =
                    [
                        Task(stageOneId, "reserve-inventory", 1),
                        Task(stageOneId, "authorize-payment", 2),
                        Task(stageOneId, "notify-customer", 3, parallelGroupId),
                        Task(stageOneId, "audit-sale", 4, parallelGroupId)
                    ]
                },
                new StageDefinition
                {
                    Id = stageTwoId,
                    OrchestrationVersionId = versionId,
                    Key = "fulfillment",
                    Name = "Fulfillment",
                    Order = 2,
                    TaskDefinitions =
                    [
                        Task(stageTwoId, "close-sale", 1)
                    ]
                }
            ]
        };
    }

    private static TaskDefinition Task(Id stageId, string key, int order, Id? parallelGroupId = null)
        => new()
        {
            Id = Id.New(),
            StageDefinitionId = stageId,
            Key = key,
            Name = key,
            Order = order,
            Kind = TaskKind.Messaging,
            ExecutionMode = parallelGroupId.HasValue ? TaskExecutionMode.Parallel : TaskExecutionMode.Sequential,
            ParallelGroupId = parallelGroupId,
            IsEnabled = true,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = $"commands.{key}",
                Version = new SemanticVersion(1, 0, 0),
                RequestSchemaBinding = Binding($"{key}.request", SchemaContractKind.CommandRequest),
                ResponseSchemaBinding = Binding($"{key}.response", SchemaContractKind.CommandResponse)
            }
        };

    private static CompensationDefinition Compensation(string topic)
        => new()
        {
            CompensationTaskKind = TaskKind.Messaging,
            DispatchType = TaskDispatchType.FireAndForget,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = topic,
                Version = new SemanticVersion(1, 0, 0),
                RequestSchemaBinding = Binding($"{topic}.request", SchemaContractKind.CommandRequest)
            }
        };

    private static OrchestrationVersion CreateVersionWithRepeatedTaskKey()
    {
        var versionId = Id.New();
        var stageOneId = Id.New();
        var stageTwoId = Id.New();
        var stageThreeId = Id.New();

        return new OrchestrationVersion
        {
            Id = versionId,
            OrchestrationDefinitionId = Id.New(),
            Version = new SemanticVersion(1, 2, 0),
            Status = OrchestrationVersionStatus.Draft,
            Checksum = new Checksum("schema-context-ambiguous-fixture"),
            CreatedBy = "tests",
            CreatedOnUtc = DateTime.UtcNow,
            TriggerBindings =
            [
                new TriggerBinding
                {
                    Id = Id.New(),
                    OrchestrationVersionId = versionId,
                    Key = "sale-created",
                    TriggerType = TriggerType.Event,
                    IsEnabled = true,
                    TriggerChannel = new EventTriggerChannel
                    {
                        Topic = "events.sales.sale.created",
                        Version = new SemanticVersion(1, 2, 0),
                        SchemaBinding = Binding("sales.sale.created", SchemaContractKind.Event)
                    }
                }
            ],
            StageDefinitions =
            [
                new StageDefinition
                {
                    Id = stageOneId,
                    OrchestrationVersionId = versionId,
                    Key = "intake",
                    Name = "Intake",
                    Order = 1,
                    TaskDefinitions = [Task(stageOneId, "audit", 1)]
                },
                new StageDefinition
                {
                    Id = stageTwoId,
                    OrchestrationVersionId = versionId,
                    Key = "enrichment",
                    Name = "Enrichment",
                    Order = 2,
                    TaskDefinitions = [Task(stageTwoId, "audit", 1)]
                },
                new StageDefinition
                {
                    Id = stageThreeId,
                    OrchestrationVersionId = versionId,
                    Key = "fulfillment",
                    Name = "Fulfillment",
                    Order = 3,
                    TaskDefinitions = [Task(stageThreeId, "close-sale", 1)]
                }
            ]
        };
    }

    private static SchemaBinding Binding(string key, SchemaContractKind kind)
        => new()
        {
            Id = Id.New(),
            ElementType = ElementType.Task,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = key,
            ContractVersion = new SemanticVersion(1, 0, 0),
            ContractKind = kind,
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "atlas",
            IsValidationEnabled = true,
            Snapshot = new ControlPlaneSchemaContractSnapshot
            {
                ContractKind = kind,
                ContentHash = $"{key}.hash",
                SchemaJson = "{}",
                ResolvedAtUtc = DateTimeOffset.UtcNow
            }
        };

    private static SchemaBinding BindingWithoutSnapshot(string key, SchemaContractKind kind)
        => new()
        {
            Id = Id.New(),
            ElementType = ElementType.Task,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = key,
            ContractVersion = new SemanticVersion(1, 0, 0),
            ContractKind = kind,
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "atlas",
            IsValidationEnabled = false
        };

    private static MetadataDescriptor MetadataDescriptor(string key, string? sourceKey = null)
        => new()
        {
            Id = Id.New(),
            Key = key,
            SourceKey = sourceKey ?? key,
            DisplayName = key,
            SchemaJson = $"{{\"type\":\"object\",\"properties\":{{\"{key}Id\":{{\"type\":\"string\"}}}}}}",
            ContentHash = $"{key}-hash",
            CreatedOnUtc = DateTime.UtcNow
        };
}
