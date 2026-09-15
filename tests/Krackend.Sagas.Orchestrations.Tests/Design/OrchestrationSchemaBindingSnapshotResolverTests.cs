namespace Krackend.Sagas.Orchestrations.Tests.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using NSubstitute;
using DesignSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;
using RegistrySchemaContractSnapshot = Krackend.Sagas.Orchestrations.SchemaRegistry.SchemaContractSnapshot;

public sealed class OrchestrationSchemaBindingSnapshotResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenProviderResolvesSnapshot_EmbedsSnapshotInBinding()
    {
        var version = CreateVersion(CreateBinding("sales.sale.created", SchemaContractKind.Event));
        var resolver = Substitute.For<ISchemaContractResolver>();
        resolver
            .ResolveAsync(Arg.Any<SchemaContractResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(SchemaContractResolutionResult.Resolved(new RegistrySchemaContractSnapshot
            {
                Reference = new SchemaContractReference
                {
                    ProviderId = "provider-001",
                    ProviderKey = "knowl",
                    ContractId = "contract-001",
                    ContractKey = "sales.sale.created",
                    ContractVersion = "1.0.0",
                    Kind = SchemaContractKind.Event
                },
                SchemaFormat = "JsonSchema",
                SchemaJson = """{"type":"object"}""",
                ContentHash = "sales.sale.created.hash",
                SourceArtifactId = "artifact-001",
                ResolvedBy = "knowl",
                ResolvedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
            }));
        var catalog = Substitute.For<ISchemaContractResolverCatalog>();
        catalog.GetResolver(Arg.Any<SchemaContractReference>()).Returns(resolver);
        var snapshotResolver = new OrchestrationSchemaBindingSnapshotResolver(catalog);

        await snapshotResolver.ResolveAsync(version);

        var eventChannel = (EventTriggerChannel)version.TriggerBindings[0].TriggerChannel;
        Assert.Equal("sales.sale.created.hash", eventChannel.SchemaBinding.Snapshot.ContentHash);
        Assert.Equal("JsonSchema", eventChannel.SchemaBinding.Snapshot.SchemaFormat);
        Assert.Equal("""{"type":"object"}""", eventChannel.SchemaBinding.Snapshot.SchemaJson);
        Assert.Equal("provider-001", eventChannel.SchemaBinding.Snapshot.RegistryProviderId);
        Assert.Equal("knowl", eventChannel.SchemaBinding.Snapshot.RegistryProviderKey);
        Assert.Equal("contract-001", eventChannel.SchemaBinding.Snapshot.ContractId);
        Assert.Equal("sales.sale.created", eventChannel.SchemaBinding.Snapshot.ContractKey);
        Assert.Equal("1.0.0", eventChannel.SchemaBinding.Snapshot.ContractVersion);
        Assert.Equal("artifact-001", eventChannel.SchemaBinding.Snapshot.SourceArtifactId);
        Assert.Equal("knowl", eventChannel.SchemaBinding.Snapshot.ResolvedBy);
    }

    [Fact]
    public async Task ResolveAsync_WhenBindingAlreadyHasSnapshot_DoesNotResolveAgain()
    {
        var binding = CreateBinding("sales.sale.created", SchemaContractKind.Event);
        binding.Snapshot = new DesignSchemaContractSnapshot
        {
            ContractKind = SchemaContractKind.Event,
            ContentHash = "existing.hash"
        };
        var version = CreateVersion(binding);
        var resolver = Substitute.For<ISchemaContractResolver>();
        var catalog = Substitute.For<ISchemaContractResolverCatalog>();
        catalog.GetResolver(Arg.Any<SchemaContractReference>()).Returns(resolver);
        var snapshotResolver = new OrchestrationSchemaBindingSnapshotResolver(catalog);

        await snapshotResolver.ResolveAsync(version);

        Assert.Equal("existing.hash", binding.Snapshot.ContentHash);
        await resolver.DidNotReceive().ResolveAsync(
            Arg.Is<SchemaContractResolutionRequest>(request =>
                request.Reference.ContractKey == "sales.sale.created"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenStrictBindingCannotBeResolved_ThrowsSchemaRegistryException()
    {
        var version = CreateVersion(CreateBinding("sales.sale.created", SchemaContractKind.Event, strictMode: true));
        var resolver = Substitute.For<ISchemaContractResolver>();
        resolver
            .ResolveAsync(Arg.Any<SchemaContractResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.NotFound,
                "Contract was not found."));
        var catalog = Substitute.For<ISchemaContractResolverCatalog>();
        catalog.GetResolver(Arg.Any<SchemaContractReference>()).Returns(resolver);
        var snapshotResolver = new OrchestrationSchemaBindingSnapshotResolver(catalog);

        var exception = await Assert.ThrowsAsync<SchemaRegistryException>(
            () => snapshotResolver.ResolveAsync(version));

        Assert.Contains("sales.sale.created", exception.Message, StringComparison.Ordinal);
    }

    private static OrchestrationVersion CreateVersion(SchemaBinding triggerBinding)
    {
        var versionId = Id.New();
        var stageId = Id.New();
        var taskId = Id.New();

        return new OrchestrationVersion
        {
            Id = versionId,
            OrchestrationDefinitionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            Status = OrchestrationVersionStatus.Approved,
            Checksum = new Checksum("snapshot-resolver"),
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
                        Version = new SemanticVersion(1, 0, 0),
                        SchemaBinding = triggerBinding
                    }
                }
            ],
            StageDefinitions =
            [
                new StageDefinition
                {
                    Id = stageId,
                    OrchestrationVersionId = versionId,
                    Key = "fulfillment",
                    Name = "Fulfillment",
                    Order = 1,
                    TaskDefinitions =
                    [
                        new TaskDefinition
                        {
                            Id = taskId,
                            StageDefinitionId = stageId,
                            Key = "inventories.reserve",
                            Name = "Reserve inventory",
                            Order = 1,
                            Kind = TaskKind.Messaging,
                            IsEnabled = true,
                            Configuration = new MessagingTaskConfiguration
                            {
                                Topic = "commands.inventories.reserve",
                                Version = new SemanticVersion(1, 0, 0),
                                RequestSchemaBinding = CreateBinding("inventories.reserve.request", SchemaContractKind.CommandRequest),
                                ResponseSchemaBinding = CreateBinding("inventories.reserve.response", SchemaContractKind.CommandResponse)
                            }
                        }
                    ]
                }
            ]
        };
    }

    private static SchemaBinding CreateBinding(
        string contractKey,
        SchemaContractKind contractKind,
        bool strictMode = false)
        => new()
        {
            Id = Id.New(),
            ElementType = ElementType.Task,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = contractKey,
            ContractVersion = new SemanticVersion(1, 0, 0),
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "snapshot",
            ContractKind = contractKind,
            StrictMode = strictMode,
            IsValidationEnabled = strictMode
        };
}
