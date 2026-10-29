using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
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
        var packageJson = CreatePackageJson().Replace("\"SchemaVersion\":1", "\"SchemaVersion\":99", StringComparison.Ordinal);

        var result = service.Inspect(packageJson);

        Assert.False(result.IsValid);
        Assert.Contains("not supported", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsNewOrchestrationCreatesLocalOwnerAndDraftAsOneTransaction()
    {
        var definitions = Substitute.For<IOrchestrationDefinitionRepository>();
        var versions = Substitute.For<IOrchestrationVersionRepository>();
        var domains = Substitute.For<IDomainRepository>();
        var teams = Substitute.For<ITeamRepository>();
        var unitOfWork = Substitute.For<IControlPlaneUnitOfWork>();
        var createdDefinitions = new List<OrchestrationDefinition>();
        var createdVersions = new List<OrchestrationVersion>();
        var createdStages = new List<StageDefinition>();

        var domainId = Id.New();
        var teamId = Id.New();
        domains.GetById(domainId, Arg.Any<CancellationToken>()).Returns(new Domain { Id = domainId, Key = "sales", DisplayName = "Sales", IsActive = true });
        teams.GetById(teamId, Arg.Any<CancellationToken>()).Returns(new Team { Id = teamId, Key = "platform", DisplayName = "Platform", IsActive = true });
        definitions.Create(Arg.Do<OrchestrationDefinition>(createdDefinitions.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        versions.Create(Arg.Do<OrchestrationVersion>(createdVersions.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var stages = Substitute.For<IStageRepository>();
        stages.Create(Arg.Do<StageDefinition>(createdStages.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task>>(0)(call.ArgAt<CancellationToken>(1)));

        var service = CreateService(definitions, versions, stages, domains, teams, unitOfWork);

        var imported = await service.ImportAsNewOrchestrationAsync(
            new CreateImportedOrchestrationInput("Imported sales flow", domainId.ToString(), teamId.ToString(), "Imported safely.", ["Sales", "portable"]),
            CreatePackageJson(),
            "operator");

        var definition = Assert.Single(createdDefinitions);
        var version = Assert.Single(createdVersions);
        Assert.Equal("sales.sale.created", definition.Key);
        Assert.Equal("Imported sales flow", definition.Name);
        Assert.Equal("Sales", definition.DomainDisplayName);
        Assert.Equal("Platform", definition.OwnerTeamDisplayName);
        Assert.Equal(["Sales", "portable"], definition.Tags);
        Assert.Equal(definition.Id, version.OrchestrationDefinitionId);
        Assert.Equal(OrchestrationVersionStatus.Draft, version.Status);
        Assert.Equal(definition.Id.ToString(), imported.OrchestrationDefinitionId);
        Assert.Equal(version.Id.ToString(), imported.OrchestrationVersionId);
        Assert.Single(createdStages);
    }

    private static OrchestrationVersionPackageService CreateService(
        IOrchestrationDefinitionRepository? definitions = null,
        IOrchestrationVersionRepository? versions = null,
        IStageRepository? stages = null,
        IDomainRepository? domains = null,
        ITeamRepository? teams = null,
        IControlPlaneUnitOfWork? unitOfWork = null)
        => new(
            definitions ?? Substitute.For<IOrchestrationDefinitionRepository>(),
            versions ?? Substitute.For<IOrchestrationVersionRepository>(),
            Substitute.For<ITriggerBindingRepository>(),
            Substitute.For<IVariableDefinitionRepository>(),
            stages ?? Substitute.For<IStageRepository>(),
            Substitute.For<ITaskRepository>(),
            Substitute.For<IParallelGroupRepository>(),
            Substitute.For<IBranchRuleRepository>(),
            Substitute.For<IMetadataDescriptorRepository>(),
            domains ?? Substitute.For<IDomainRepository>(),
            teams ?? Substitute.For<ITeamRepository>(),
            Substitute.For<IOrchestrationArtifactPayloadFactory>(),
            unitOfWork ?? Substitute.For<IControlPlaneUnitOfWork>());

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
            Source = new("sales.sale.created", Id.New().ToString(), "Draft", string.Empty),
            ArtifactJson = artifactJson,
            ArtifactChecksum = $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(artifactJson))).ToLowerInvariant()}",
        };
        return JsonSerializer.Serialize(package);
    }
}
