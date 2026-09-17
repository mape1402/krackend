using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using ControlPlaneSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class OrchestrationSchemaContextBuilderTests
{
    [Fact]
    public async Task BuildForTask_ExposesOnlyTriggerForFirstTask()
    {
        var version = CreateVersion();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, version.StageDefinitions[0].TaskDefinitions[0].Id);

        var source = Assert.Single(context.Sources);
        Assert.Equal("trigger", source.Alias);
        Assert.Equal(SchemaContractKind.Event, source.SchemaBinding.ContractKind);
        Assert.Equal("stages.intake.tasks.reserve-inventory.request", context.Target.Alias);
        Assert.Equal(SchemaContractKind.CommandRequest, context.Target.SchemaBinding.ContractKind);
        Assert.False(string.IsNullOrWhiteSpace(context.Signature));
    }

    [Fact]
    public async Task BuildForTask_ExposesPreviousSequentialResponses()
    {
        var version = CreateVersion();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, version.StageDefinitions[0].TaskDefinitions[1].Id);

        Assert.Contains(context.Sources, x => x.Alias == "trigger");
        Assert.Contains(context.Sources, x => x.Alias == "stages.intake.tasks.reserve-inventory.response");
        Assert.Contains(context.Sources, x => x.Alias == "tasks.reserve-inventory.response");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "stages.intake.tasks.authorize-payment.response");
    }

    [Fact]
    public async Task BuildForTask_DoesNotExposeParallelSiblingResponses()
    {
        var version = CreateVersion();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, version.StageDefinitions[0].TaskDefinitions[2].Id);

        Assert.Contains(context.Sources, x => x.Alias == "trigger");
        Assert.Contains(context.Sources, x => x.Alias == "stages.intake.tasks.reserve-inventory.response");
        Assert.Contains(context.Sources, x => x.Alias == "stages.intake.tasks.authorize-payment.response");
        Assert.Contains(context.Sources, x => x.Alias == "tasks.reserve-inventory.response");
        Assert.Contains(context.Sources, x => x.Alias == "tasks.authorize-payment.response");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "stages.intake.tasks.notify-customer.response");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "stages.intake.tasks.audit-sale.response");
    }

    [Fact]
    public async Task BuildForTask_ExposesPreviousStageAndJoinedParallelResponses()
    {
        var version = CreateVersion();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, version.StageDefinitions[1].TaskDefinitions[0].Id);

        Assert.Contains(context.Sources, x => x.Alias == "trigger");
        Assert.Contains(context.Sources, x => x.Alias == "stages.intake.tasks.reserve-inventory.response");
        Assert.Contains(context.Sources, x => x.Alias == "stages.intake.tasks.authorize-payment.response");
        Assert.Contains(context.Sources, x => x.Alias == "stages.intake.tasks.notify-customer.response");
        Assert.Contains(context.Sources, x => x.Alias == "stages.intake.tasks.audit-sale.response");
        Assert.Contains(context.Sources, x => x.Alias == "tasks.notify-customer.response");
        Assert.Contains(context.Sources, x => x.Alias == "tasks.audit-sale.response");
        Assert.Equal("stages.fulfillment.tasks.close-sale.request", context.Target.Alias);
    }

    [Fact]
    public async Task BuildForTask_DoesNotExposeShortAliasWhenTaskKeyIsAmbiguous()
    {
        var version = CreateVersionWithRepeatedTaskKey();
        var builder = new OrchestrationSchemaContextBuilder();

        var context = await builder.BuildForTask(version, version.StageDefinitions[2].TaskDefinitions[0].Id);

        Assert.Contains(context.Sources, x => x.Alias == "stages.intake.tasks.audit.response");
        Assert.Contains(context.Sources, x => x.Alias == "stages.enrichment.tasks.audit.response");
        Assert.DoesNotContain(context.Sources, x => x.Alias == "tasks.audit.response");
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

        Assert.Empty(context.Sources);
        Assert.Null(context.Target.SchemaBinding);
        Assert.False(string.IsNullOrWhiteSpace(context.Signature));
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
}
