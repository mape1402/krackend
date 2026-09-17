namespace Krackend.Sagas.Orchestrations.Tests.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
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
    public async Task ResolveAsync_WhenBindingAlreadyHasMatchingSnapshotMetadata_DoesNotResolveAgain()
    {
        var binding = CreateBinding("sales.sale.created", SchemaContractKind.Event);
        binding.Snapshot = new DesignSchemaContractSnapshot
        {
            ContractKind = SchemaContractKind.Event,
            RegistryProviderKey = binding.RegistryProviderKey,
            ContractKey = binding.ContractKey,
            ContractVersion = binding.ContractVersion.ToString(),
            ContentHash = "existing.hash"
        };
        var version = CreateVersion(binding);
        version.StageDefinitions.Clear();
        var resolver = Substitute.For<ISchemaContractResolver>();
        var catalog = Substitute.For<ISchemaContractResolverCatalog>();
        catalog.GetResolver(Arg.Any<SchemaContractReference>()).Returns(resolver);
        var snapshotResolver = new OrchestrationSchemaBindingSnapshotResolver(catalog);

        await snapshotResolver.ResolveAsync(version);

        Assert.Equal("existing.hash", binding.Snapshot.ContentHash);
        await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default);
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

    [Fact]
    public async Task ResolveAsync_ResolvesCompensationRequestAndResponseBindings()
    {
        var version = CreateVersion(CreateBinding("sales.sale.created", SchemaContractKind.Event));
        var task = version.StageDefinitions[0].TaskDefinitions[0];
        var compensationRequest = CreateBinding("inventories.release.request", SchemaContractKind.CommandRequest);
        var compensationResponse = CreateBinding("inventories.release.response", SchemaContractKind.CommandResponse);
        task.CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Messaging,
            DispatchType = TaskDispatchType.FireAndForget,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "commands.inventories.release",
                Version = new SemanticVersion(1, 0, 0),
                RequestSchemaBinding = compensationRequest,
                ResponseSchemaBinding = compensationResponse
            }
        };
        var resolver = Substitute.For<ISchemaContractResolver>();
        resolver
            .ResolveAsync(Arg.Any<SchemaContractResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var reference = call.Arg<SchemaContractResolutionRequest>().Reference;
                return SchemaContractResolutionResult.Resolved(new RegistrySchemaContractSnapshot
                {
                    Reference = reference,
                    SchemaFormat = "ButterMorph",
                    SchemaJson = """{"type":"object"}""",
                    ContentHash = $"{reference.ContractKey}.hash",
                    SourceArtifactId = $"artifact-{reference.ContractKey}",
                    ResolvedBy = "tests",
                    ResolvedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
                });
            });
        var catalog = Substitute.For<ISchemaContractResolverCatalog>();
        catalog.GetResolver(Arg.Any<SchemaContractReference>()).Returns(resolver);
        var snapshotResolver = new OrchestrationSchemaBindingSnapshotResolver(catalog);

        await snapshotResolver.ResolveAsync(version);

        Assert.Equal("inventories.release.request.hash", compensationRequest.Snapshot!.ContentHash);
        Assert.Equal(SchemaContractKind.CommandRequest, compensationRequest.Snapshot.ContractKind);
        Assert.Equal("inventories.release.response.hash", compensationResponse.Snapshot!.ContentHash);
        Assert.Equal(SchemaContractKind.CommandResponse, compensationResponse.Snapshot.ContractKind);
        await resolver.Received().ResolveAsync(
            Arg.Is<SchemaContractResolutionRequest>(request =>
                request.Reference.ContractKey == "inventories.release.request" &&
                request.Reference.Kind == SchemaContractKind.CommandRequest),
            Arg.Any<CancellationToken>());
        await resolver.Received().ResolveAsync(
            Arg.Is<SchemaContractResolutionRequest>(request =>
                request.Reference.ContractKey == "inventories.release.response" &&
                request.Reference.Kind == SchemaContractKind.CommandResponse),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenMessagingUsesCommandBinding_ExpandsRequestAndResponseSnapshotsFromSameCommand()
    {
        var version = CreateVersion(CreateBinding("sales.sale.created", SchemaContractKind.Event));
        var task = version.StageDefinitions[0].TaskDefinitions[0];
        var commandBinding = CreateBinding("inventories.reserve", SchemaContractKind.Command);
        task.Configuration = new MessagingTaskConfiguration
        {
            Topic = "commands.inventories.reserve",
            Version = new SemanticVersion(1, 0, 0),
            SchemaBinding = commandBinding,
            HasSchemaValidation = true,
            HasResponseValidation = true
        };
        var resolver = Substitute.For<ISchemaContractResolver>();
        resolver
            .ResolveAsync(Arg.Any<SchemaContractResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var reference = call.Arg<SchemaContractResolutionRequest>().Reference;
                return SchemaContractResolutionResult.Resolved(new RegistrySchemaContractSnapshot
                {
                    Reference = reference,
                    SchemaFormat = "ButterMorph",
                    SchemaJson = """{"type":"object"}""",
                    ContentHash = $"{reference.ContractKey}.{reference.Kind}.hash",
                    SourceArtifactId = $"artifact-{reference.Kind}",
                    ResolvedBy = "tests",
                    ResolvedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
                });
            });
        var catalog = Substitute.For<ISchemaContractResolverCatalog>();
        catalog.GetResolver(Arg.Any<SchemaContractReference>()).Returns(resolver);
        var snapshotResolver = new OrchestrationSchemaBindingSnapshotResolver(catalog);

        await snapshotResolver.ResolveAsync(version);

        var messaging = Assert.IsType<MessagingTaskConfiguration>(task.Configuration);
        Assert.NotNull(messaging.RequestSchemaBinding);
        Assert.NotNull(messaging.ResponseSchemaBinding);
        Assert.Equal(SchemaContractKind.CommandRequest, messaging.RequestSchemaBinding.ContractKind);
        Assert.Equal(SchemaContractKind.CommandResponse, messaging.ResponseSchemaBinding.ContractKind);
        Assert.Equal("inventories.reserve.CommandRequest.hash", messaging.RequestSchemaBinding.Snapshot!.ContentHash);
        Assert.Equal("inventories.reserve.CommandResponse.hash", messaging.ResponseSchemaBinding.Snapshot!.ContentHash);
        await resolver.Received().ResolveAsync(
            Arg.Is<SchemaContractResolutionRequest>(request =>
                request.Reference.ContractKey == "inventories.reserve" &&
                request.Reference.Kind == SchemaContractKind.CommandRequest),
            Arg.Any<CancellationToken>());
        await resolver.Received().ResolveAsync(
            Arg.Is<SchemaContractResolutionRequest>(request =>
                request.Reference.ContractKey == "inventories.reserve" &&
                request.Reference.Kind == SchemaContractKind.CommandResponse),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenCommandReplyIsMissingAndResponseValidationIsDisabled_KeepsRequestSnapshot()
    {
        var version = CreateVersion(CreateBinding("sales.sale.created", SchemaContractKind.Event));
        var task = version.StageDefinitions[0].TaskDefinitions[0];
        task.Configuration = new MessagingTaskConfiguration
        {
            Topic = "commands.inventories.reserve",
            Version = new SemanticVersion(1, 0, 0),
            SchemaBinding = CreateBinding("inventories.reserve", SchemaContractKind.Command),
            HasSchemaValidation = true,
            HasResponseValidation = false
        };
        var resolver = Substitute.For<ISchemaContractResolver>();
        resolver
            .ResolveAsync(Arg.Any<SchemaContractResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var reference = call.Arg<SchemaContractResolutionRequest>().Reference;
                return reference.Kind == SchemaContractKind.CommandResponse
                    ? SchemaContractResolutionResult.Failed(SchemaContractResolutionStatus.NotFound, "Reply artifact was not found.")
                    : SchemaContractResolutionResult.Resolved(new RegistrySchemaContractSnapshot
                    {
                        Reference = reference,
                        SchemaFormat = "ButterMorph",
                        SchemaJson = """{"type":"object"}""",
                        ContentHash = "request.hash",
                        SourceArtifactId = "request-artifact",
                        ResolvedBy = "tests",
                        ResolvedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
                    });
            });
        var catalog = Substitute.For<ISchemaContractResolverCatalog>();
        catalog.GetResolver(Arg.Any<SchemaContractReference>()).Returns(resolver);
        var snapshotResolver = new OrchestrationSchemaBindingSnapshotResolver(catalog);

        await snapshotResolver.ResolveAsync(version);

        var messaging = Assert.IsType<MessagingTaskConfiguration>(task.Configuration);
        Assert.Equal("request.hash", messaging.RequestSchemaBinding.Snapshot!.ContentHash);
        Assert.Null(messaging.ResponseSchemaBinding.Snapshot);
    }

    [Fact]
    public async Task ResolveAsync_AllowsOptionalBindingWithoutProviderButRejectsRequiredBindingWithoutProvider()
    {
        var optional = CreateBinding("optional.command", SchemaContractKind.CommandRequest);
        optional.RegistryProviderKey = string.Empty;
        optional.StrictMode = false;
        optional.IsValidationEnabled = false;
        var required = CreateBinding("required.command", SchemaContractKind.CommandRequest);
        required.RegistryProviderKey = string.Empty;
        required.StrictMode = false;
        required.IsValidationEnabled = true;
        var resolver = Substitute.For<ISchemaContractResolver>();
        var catalog = Substitute.For<ISchemaContractResolverCatalog>();
        catalog.GetResolver(Arg.Any<SchemaContractReference>()).Returns(resolver);
        var snapshotResolver = new OrchestrationSchemaBindingSnapshotResolver(catalog);

        var optionalVersion = CreateVersion(optional);
        optionalVersion.StageDefinitions.Clear();
        var requiredVersion = CreateVersion(required);
        requiredVersion.StageDefinitions.Clear();

        await snapshotResolver.ResolveAsync(optionalVersion);

        await Assert.ThrowsAsync<SchemaRegistryException>(() => snapshotResolver.ResolveAsync(requiredVersion));
        await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default);
    }

    [Fact]
    public async Task SchemaContextApplicationServiceLoadsVersionAndDelegatesToBuilder()
    {
        var taskId = Id.New();
        var version = CreateVersion(CreateBinding("sales.sale.created", SchemaContractKind.Event));
        var repository = Substitute.For<IOrchestrationVersionRepository>();
        var builder = Substitute.For<IOrchestrationSchemaContextBuilder>();
        repository.GetById(version.Id, Arg.Any<CancellationToken>()).Returns(version);
        builder
            .BuildForTask(version, taskId, Arg.Any<CancellationToken>())
            .Returns(new OrchestrationSchemaContext
            {
                OrchestrationVersionId = version.Id.ToString(),
                OrchestrationVersion = version.Version.ToString(),
                StageKey = "fulfillment",
                Signature = "signature-1",
                TaskKey = "inventories.reserve"
            });
        var service = new OrchestrationSchemaContextApplicationService(repository, builder);

        var context = await service.GetForTask(new GetTaskSchemaContextQuery(version.Id.ToString(), taskId.ToString()));

        Assert.Equal("inventories.reserve", context.TaskKey);
        await repository.Received(1).GetById(version.Id, Arg.Any<CancellationToken>());
        await builder.Received(1).BuildForTask(version, taskId, Arg.Any<CancellationToken>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetForTask(null!));
    }

    [Fact]
    public async Task SchemaContextApplicationServiceResolvesSnapshotsBeforeBuildingContext()
    {
        var taskId = Id.New();
        var version = CreateVersion(CreateBinding("sales.sale.created", SchemaContractKind.Event));
        var repository = Substitute.For<IOrchestrationVersionRepository>();
        var snapshotResolver = Substitute.For<IOrchestrationSchemaBindingSnapshotResolver>();
        var builder = Substitute.For<IOrchestrationSchemaContextBuilder>();
        repository.GetById(version.Id, Arg.Any<CancellationToken>()).Returns(version);
        builder
            .BuildForTask(version, taskId, Arg.Any<CancellationToken>())
            .Returns(new OrchestrationSchemaContext
            {
                OrchestrationVersionId = version.Id.ToString(),
                OrchestrationVersion = version.Version.ToString(),
                StageKey = "fulfillment",
                Signature = "signature-1",
                TaskKey = "inventories.reserve"
            });
        var service = new OrchestrationSchemaContextApplicationService(repository, builder, snapshotResolver);

        var context = await service.GetForTask(new GetTaskSchemaContextQuery(version.Id.ToString(), taskId.ToString()));

        Assert.Equal("inventories.reserve", context.TaskKey);
        await snapshotResolver.Received(1).ResolveAsync(version, Arg.Any<CancellationToken>());
        await builder.Received(1).BuildForTask(version, taskId, Arg.Any<CancellationToken>());
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
