using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
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

    private static OrchestrationVersionPackageService CreateService()
        => new(
            Substitute.For<IOrchestrationDefinitionRepository>(),
            Substitute.For<IOrchestrationVersionRepository>(),
            Substitute.For<ITriggerBindingRepository>(),
            Substitute.For<IVariableDefinitionRepository>(),
            Substitute.For<IStageRepository>(),
            Substitute.For<ITaskRepository>(),
            Substitute.For<IParallelGroupRepository>(),
            Substitute.For<IBranchRuleRepository>(),
            Substitute.For<IMetadataDescriptorRepository>(),
            Substitute.For<IOrchestrationArtifactPayloadFactory>(),
            Substitute.For<IControlPlaneUnitOfWork>());

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
