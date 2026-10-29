using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class OrchestrationVersionPackageServiceTests
{
    [Fact]
    public void InspectAcceptsACurrentIntegrityProtectedPackage()
    {
        var service = CreateService();

        var result = service.Inspect(CreatePackageJson());

        Assert.True(result.IsValid);
        Assert.Equal("sales.sale.created", result.OrchestrationKey);
        Assert.Equal(1, result.StageCount);
        Assert.Equal(1, result.TaskCount);
    }

    [Fact]
    public void InspectRejectsAPackageWhoseArtifactChecksumWasChanged()
    {
        var service = CreateService();
        var packageJson = CreatePackageJson().Replace("sha256:", "sha256:changed", StringComparison.Ordinal);

        var result = service.Inspect(packageJson);

        Assert.False(result.IsValid);
        Assert.Contains("checksum", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InspectRejectsAnUnknownPackageSchema()
    {
        var service = CreateService();
        var packageJson = CreatePackageJson().Replace("\"SchemaVersion\":2", "\"SchemaVersion\":99", StringComparison.Ordinal);

        var result = service.Inspect(packageJson);

        Assert.False(result.IsValid);
        Assert.Contains("not supported", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InspectRejectsAPackageWithoutSourceVersionIdentity()
    {
        var service = CreateService();
        var package = JsonNode.Parse(CreatePackageJson())!.AsObject();
        package["Source"] = null;

        var result = service.Inspect(package.ToJsonString());

        Assert.False(result.IsValid);
        Assert.Contains("source orchestration version", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportCreatesDraftInsideTheExistingOrchestrationAsOneTransaction()
    {
        var definitions = Substitute.For<IOrchestrationDefinitionRepository>();
        var versions = Substitute.For<IOrchestrationVersionRepository>();
        var unitOfWork = Substitute.For<IControlPlaneUnitOfWork>();
        var createdVersions = new List<OrchestrationVersion>();
        var createdStages = new List<StageDefinition>();

        var definitionId = Id.New();
        definitions.GetById(definitionId, Arg.Any<CancellationToken>()).Returns(CreateDefinition(definitionId));
        versions.GetAll(definitionId, Arg.Any<PagedSettings>(), Arg.Any<CancellationToken>()).Returns(EmptyVersions());
        versions.Create(Arg.Do<OrchestrationVersion>(createdVersions.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var stages = Substitute.For<IStageRepository>();
        stages.Create(Arg.Do<StageDefinition>(createdStages.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task>>(0)(call.ArgAt<CancellationToken>(1)));

        var service = CreateService(definitions, versions, stages, unitOfWork);

        var importedVersionId = await service.ImportAsync(definitionId.ToString(), CreatePackageJson(), "operator");

        var version = Assert.Single(createdVersions);
        Assert.Equal(definitionId, version.OrchestrationDefinitionId);
        Assert.Equal(OrchestrationVersionStatus.Draft, version.Status);
        Assert.Equal(version.Id.ToString(), importedVersionId);
        Assert.Single(createdStages);
    }

    [Fact]
    public async Task ImportRejectsAPackageForADifferentOrchestrationBeforeWritingAnything()
    {
        var definitionId = Id.New();
        var definitions = Substitute.For<IOrchestrationDefinitionRepository>();
        var versions = Substitute.For<IOrchestrationVersionRepository>();
        definitions.GetById(definitionId, Arg.Any<CancellationToken>()).Returns(CreateDefinition(definitionId, "inventory.stock.updated"));

        var service = CreateService(definitions: definitions, versions: versions);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ImportAsync(definitionId.ToString(), CreatePackageJson(), "operator"));

        Assert.Contains("different orchestration key", exception.Message, StringComparison.OrdinalIgnoreCase);
        await versions.DidNotReceive().Create(Arg.Any<OrchestrationVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportRejectsWhenTheDestinationAlreadyHasAnActiveDraft()
    {
        var definitionId = Id.New();
        var definitions = Substitute.For<IOrchestrationDefinitionRepository>();
        var versions = Substitute.For<IOrchestrationVersionRepository>();
        definitions.GetById(definitionId, Arg.Any<CancellationToken>()).Returns(CreateDefinition(definitionId));
        versions.GetAll(definitionId, Arg.Any<PagedSettings>(), Arg.Any<CancellationToken>()).Returns(
            new PagedResult<OrchestrationVersion>(
                1,
                1,
                1,
                1000,
                [CreateVersion(Id.New(), definitionId, OrchestrationVersionStatus.Draft)]));

        var service = CreateService(definitions: definitions, versions: versions);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ImportAsync(definitionId.ToString(), CreatePackageJson(), "operator"));

        Assert.Contains("active Draft", exception.Message, StringComparison.OrdinalIgnoreCase);
        await versions.DidNotReceive().Create(Arg.Any<OrchestrationVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportKeepsTheSourceLifecycleAsAuditOnlyAndAlwaysCreatesADraft()
    {
        var definitions = Substitute.For<IOrchestrationDefinitionRepository>();
        var versions = Substitute.For<IOrchestrationVersionRepository>();
        var unitOfWork = Substitute.For<IControlPlaneUnitOfWork>();
        var createdVersions = new List<OrchestrationVersion>();
        var definitionId = Id.New();
        definitions.GetById(definitionId, Arg.Any<CancellationToken>()).Returns(CreateDefinition(definitionId));
        versions.GetAll(definitionId, Arg.Any<PagedSettings>(), Arg.Any<CancellationToken>()).Returns(
            EmptyVersions());
        versions.Create(Arg.Do<OrchestrationVersion>(createdVersions.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task>>(0)(call.ArgAt<CancellationToken>(1)));

        var service = CreateService(definitions: definitions, versions: versions, unitOfWork: unitOfWork);
        var deployedSource = CreatePackageJson()
            .Replace("\"Version\":\"\"", "\"Version\":\"2.4.1\"", StringComparison.Ordinal)
            .Replace("\"Status\":\"Draft\"", "\"Status\":\"Deployed\"", StringComparison.Ordinal);

        await service.ImportAsync(definitionId.ToString(), deployedSource, "operator");

        var imported = Assert.Single(createdVersions);
        Assert.Equal(OrchestrationVersionStatus.Draft, imported.Status);
        Assert.Null(imported.Version);
        Assert.Contains("2.4.1", imported.Notes, StringComparison.Ordinal);
        Assert.Contains("Deployed", imported.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void InspectRejectsAnAbandonedSourceVersion()
    {
        var service = CreateService();
        var packageJson = CreatePackageJson().Replace("\"Status\":\"Draft\"", "\"Status\":\"Abandoned\"", StringComparison.Ordinal);

        var result = service.Inspect(packageJson);

        Assert.False(result.IsValid);
        Assert.Contains("Abandoned", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportRejectsAnAbandonedVersionBeforeItBuildsTheGraph()
    {
        var versionId = Id.New();
        var definitionId = Id.New();
        var versions = Substitute.For<IOrchestrationVersionRepository>();
        versions.GetById(versionId, Arg.Any<CancellationToken>()).Returns(CreateVersion(versionId, definitionId, OrchestrationVersionStatus.Abandoned));

        var service = CreateService(versions: versions);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(versionId.ToString()));

        Assert.Contains("Abandoned", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAllowsNonAbandonedVersionAndKeepsSourceVersionAsInformationalData()
    {
        var versionId = Id.New();
        var definitionId = Id.New();
        var versions = Substitute.For<IOrchestrationVersionRepository>();
        var definitions = Substitute.For<IOrchestrationDefinitionRepository>();
        var payloads = Substitute.For<IOrchestrationArtifactPayloadFactory>();
        var version = CreateVersion(versionId, definitionId, OrchestrationVersionStatus.Deployed);
        version.Version = new SemanticVersion(2, 4, 1);
        version.VersionLabel = "stable";
        versions.GetById(versionId, Arg.Any<CancellationToken>()).Returns(version);
        definitions.GetById(definitionId, Arg.Any<CancellationToken>()).Returns(CreateDefinition(definitionId));
        payloads.CreatePayloadJson(Arg.Any<OrchestrationDefinition>(), Arg.Any<OrchestrationVersion>()).Returns("{\"kind\":\"portable\"}");

        var service = CreateService(definitions: definitions, versions: versions, payloadFactory: payloads);

        var package = await service.ExportAsync(versionId.ToString());

        Assert.Equal(OrchestrationVersionPackage.CurrentSchemaVersion, package.SchemaVersion);
        Assert.Equal(OrchestrationVersionPackage.Kind, package.PackageKind);
        Assert.Equal("2.4.1", package.Source.Version);
        Assert.Equal("Deployed", package.Source.Status);
        Assert.Equal("stable", package.Source.Label);
        Assert.Equal("sha256:66dbce477a155b8d58cc5c4a007df1d37b2fb7cbc22b2a0664bafce51ba78334", package.ArtifactChecksum);
    }

    private static OrchestrationVersionPackageService CreateService(
        IOrchestrationDefinitionRepository? definitions = null,
        IOrchestrationVersionRepository? versions = null,
        IStageRepository? stages = null,
        IControlPlaneUnitOfWork? unitOfWork = null,
        IOrchestrationArtifactPayloadFactory? payloadFactory = null)
    {
        var triggers = Substitute.For<ITriggerBindingRepository>();
        var variables = Substitute.For<IVariableDefinitionRepository>();
        var taskDefinitions = Substitute.For<ITaskRepository>();
        var groups = Substitute.For<IParallelGroupRepository>();
        var branches = Substitute.For<IBranchRuleRepository>();
        var metadata = Substitute.For<IMetadataDescriptorRepository>();
        triggers.GetAll(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IEnumerable<TriggerBinding>>([]));
        variables.GetAll(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IEnumerable<VariableDefinition>>([]));
        (stages ??= Substitute.For<IStageRepository>()).GetAll(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IEnumerable<StageDefinition>>([]));
        taskDefinitions.GetAll(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IEnumerable<TaskDefinition>>([]));
        groups.GetAll(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IEnumerable<ParallelGroupDefinition>>([]));
        branches.GetAll(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IEnumerable<BranchRuleDefinition>>([]));
        metadata.GetAllDescriptors(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<MetadataDescriptor>>([]));
        return new(
            definitions ?? Substitute.For<IOrchestrationDefinitionRepository>(),
            versions ?? Substitute.For<IOrchestrationVersionRepository>(),
            triggers,
            variables,
            stages,
            taskDefinitions,
            groups,
            branches,
            metadata,
            payloadFactory ?? Substitute.For<IOrchestrationArtifactPayloadFactory>(),
            unitOfWork ?? Substitute.For<IControlPlaneUnitOfWork>());
    }

    private static OrchestrationDefinition CreateDefinition(Id id, string key = "sales.sale.created") => new()
    {
        Id = id,
        Key = key,
        Name = "Sale created",
        CreatedBy = "test"
    };

    private static OrchestrationVersion CreateVersion(Id id, Id definitionId, OrchestrationVersionStatus status) => new()
    {
        Id = id,
        OrchestrationDefinitionId = definitionId,
        Status = status,
        Checksum = new Checksum("test"),
        CreatedBy = "test"
    };

    private static PagedResult<OrchestrationVersion> EmptyVersions() => new(1, 1, 0, 1000, []);

    private static string CreatePackageJson()
    {
        var task = new TaskArtifact(
            Id.New(), "sales.create", "Create sale", 0, string.Empty, TaskKind.HumanApproval,
            TaskExecutionMode.Sequential, null, null!, null!, new HumanApprovalTaskConfigurationArtifact(), null!, null!,
            OnErrorPolicy.Stop, null!, TaskDispatchType.FireAndForget, true);
        var artifact = new OrchestrationArtifact(
            Id.New(), Id.New(), "sales.sale.created", "Sale created", "Sales", new SemanticVersion(0, 0, 0),
            new Checksum("draft:portable"), [], [],
            [new StageArtifact(Id.New(), "sales", "Sales", 0, true, null!, [task], [], [])]);
        var artifactJson = JsonSerializer.Serialize(artifact);
        var package = new OrchestrationVersionPackage
        {
            Source = new("sales.sale.created", Id.New().ToString(), string.Empty, "Draft", string.Empty),
            ArtifactJson = artifactJson,
            ArtifactChecksum = $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(artifactJson))).ToLowerInvariant()}",
        };
        return JsonSerializer.Serialize(package);
    }
}
