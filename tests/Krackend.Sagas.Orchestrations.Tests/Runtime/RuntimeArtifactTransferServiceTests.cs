using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeArtifactTransferServiceTests
{
    [Fact]
    public async Task ExportAndInspectActiveArtifact_CreateAnIntegrityProtectedReleaseCandidate()
    {
        var artifact = CreateArtifact(RuntimeOrchestrationLifecycleStatus.Active);
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        repository.GetById(artifact.Id, Arg.Any<CancellationToken>()).Returns(artifact);
        var service = new RuntimeArtifactTransferService(repository, Substitute.For<IRuntimeArtifactDeploymentService>());

        var package = await service.ExportAsync(artifact.Id.ToString());
        var inspection = service.Inspect(service.Serialize(package));

        Assert.Equal(RuntimeArtifactTransferPackage.Kind, inspection.PackageKind);
        Assert.Equal(artifact.Id.ToString(), inspection.SourceArtifactId);
        Assert.Equal(artifact.OrchestrationDefinitionKey, inspection.OrchestrationKey);
        Assert.Equal(artifact.Version.ToString(), inspection.Version);
        Assert.Equal("orchestration-version.new", inspection.ArtifactType);
        Assert.Equal("Rollout", inspection.ReleaseStrategy);
        Assert.Equal(artifact.ArtifactPayload!["orchestrationDefinitionId"]!.GetValue<string>(), package.Delivery.OrchestrationDefinitionId);
        Assert.StartsWith("sha256:", package.PackageChecksum, StringComparison.Ordinal);
        Assert.StartsWith("sha256:", package.Delivery.Checksum, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RuntimeOrchestrationLifecycleStatus.Deprecated, "orchestration-version.deprecated", "deprecate")]
    [InlineData(RuntimeOrchestrationLifecycleStatus.Archived, "orchestration-version.archived", "archive")]
    public async Task ExportLifecycleArtifact_RebuildsThePortableLifecycleEnvelope(
        RuntimeOrchestrationLifecycleStatus lifecycle,
        string expectedType,
        string expectedOperation)
    {
        var artifact = CreateArtifact(lifecycle);
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        repository.GetById(artifact.Id, Arg.Any<CancellationToken>()).Returns(artifact);
        var service = new RuntimeArtifactTransferService(repository, Substitute.For<IRuntimeArtifactDeploymentService>());

        var package = await service.ExportAsync(artifact.Id.ToString());

        Assert.Equal(expectedType, package.Delivery.ArtifactType);
        Assert.Equal("Direct", package.Delivery.ReleaseStrategy);
        Assert.Contains($"\"operation\": \"{expectedOperation}\"", package.Delivery.PayloadJson, StringComparison.Ordinal);
        Assert.Contains(artifact.ArtifactChecksum.Value, package.Delivery.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_RejectsATamperedPackageBeforeItReachesDeployment()
    {
        var artifact = CreateArtifact(RuntimeOrchestrationLifecycleStatus.Active);
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        repository.GetById(artifact.Id, Arg.Any<CancellationToken>()).Returns(artifact);
        var deployment = Substitute.For<IRuntimeArtifactDeploymentService>();
        var service = new RuntimeArtifactTransferService(repository, deployment);
        var json = service.Serialize(await service.ExportAsync(artifact.Id.ToString()));
        var tampered = json.Replace("sales.sale.created", "payments.sale.created", StringComparison.Ordinal);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(tampered, "operator"));

        Assert.Contains("checksum", exception.Message, StringComparison.OrdinalIgnoreCase);
        await deployment.DidNotReceive().DeployAsync(Arg.Any<Krackend.Sagas.Orchestrations.Abstractions.Distribution.RuntimeArtifactDeliveryPackage>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_DelegatesAValidPackageToTheStandardDeploymentPipeline()
    {
        var artifact = CreateArtifact(RuntimeOrchestrationLifecycleStatus.Active);
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        repository.GetById(artifact.Id, Arg.Any<CancellationToken>()).Returns(artifact);
        var deployment = Substitute.For<IRuntimeArtifactDeploymentService>();
        deployment.DeployAsync(Arg.Any<Krackend.Sagas.Orchestrations.Abstractions.Distribution.RuntimeArtifactDeliveryPackage>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Krackend.Sagas.Orchestrations.Abstractions.Distribution.RuntimeArtifactDeploymentResult { Accepted = true, Status = "Pending" });
        var service = new RuntimeArtifactTransferService(repository, deployment);
        var json = service.Serialize(await service.ExportAsync(artifact.Id.ToString()));

        var result = await service.ImportAsync(json, "release-operator");

        Assert.True(result.Accepted);
        await deployment.Received(1).DeployAsync(
            Arg.Is<Krackend.Sagas.Orchestrations.Abstractions.Distribution.RuntimeArtifactDeliveryPackage>(x =>
                x.OrchestrationDefinitionKey == artifact.OrchestrationDefinitionKey && x.Version == artifact.Version.ToString()),
            "portable-import:release-operator",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_RejectsAnInvalidArtifactIdentifierBeforeQueryingStorage()
    {
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        var service = new RuntimeArtifactTransferService(repository, Substitute.For<IRuntimeArtifactDeploymentService>());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync("not-a-runtime-artifact"));

        Assert.Contains("Runtime artifact id", exception.Message, StringComparison.Ordinal);
        await repository.DidNotReceive().GetById(Arg.Any<Id>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_RejectsAnArtifactThatNoLongerExists()
    {
        var artifactId = Id.New();
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        repository.GetById(artifactId, Arg.Any<CancellationToken>()).Returns((RuntimeOrchestrationArtifact?)null);
        var service = new RuntimeArtifactTransferService(repository, Substitute.For<IRuntimeArtifactDeploymentService>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(artifactId.ToString()));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true, "payload")]
    [InlineData(false, "definition id")]
    public async Task ExportAsync_RejectsIncompleteActiveArtifactPayloads(bool missingPayload, string expectedMessage)
    {
        var artifact = CreateArtifact(RuntimeOrchestrationLifecycleStatus.Active);
        artifact.ArtifactPayload = missingPayload
            ? null!
            : new JsonObject { ["key"] = artifact.OrchestrationDefinitionKey };
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        repository.GetById(artifact.Id, Arg.Any<CancellationToken>()).Returns(artifact);
        var service = new RuntimeArtifactTransferService(repository, Substitute.For<IRuntimeArtifactDeploymentService>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(artifact.Id.ToString()));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_UsesPortableDefaultsWhenTheOriginalReleaseDetailsAreEmpty()
    {
        var artifact = CreateArtifact(RuntimeOrchestrationLifecycleStatus.Active);
        artifact.LastReleaseId = string.Empty;
        artifact.ReleaseStrategy = string.Empty;
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        repository.GetById(artifact.Id, Arg.Any<CancellationToken>()).Returns(artifact);
        var service = new RuntimeArtifactTransferService(repository, Substitute.For<IRuntimeArtifactDeploymentService>());

        var package = await service.ExportAsync(artifact.Id.ToString());

        Assert.Equal($"portable-{artifact.Id}", package.Delivery.ReleaseId);
        Assert.Equal("Direct", package.Delivery.ReleaseStrategy);
    }

    [Fact]
    public async Task ImportAsync_RequiresAnActorAndInspectExplainsMalformedJson()
    {
        var service = new RuntimeArtifactTransferService(
            Substitute.For<IRuntimeArtifactRepository>(),
            Substitute.For<IRuntimeArtifactDeploymentService>());

        var actorException = await Assert.ThrowsAsync<ArgumentException>(() => service.ImportAsync("{}", " "));
        var packageException = Assert.Throws<InvalidOperationException>(() => service.Inspect("{ not-json"));

        Assert.Contains("actor", actorException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid JSON", packageException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_RejectsUnknownArtifactLifecycle()
    {
        var artifact = CreateArtifact((RuntimeOrchestrationLifecycleStatus)99);
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        repository.GetById(artifact.Id, Arg.Any<CancellationToken>()).Returns(artifact);
        var service = new RuntimeArtifactTransferService(repository, Substitute.For<IRuntimeArtifactDeploymentService>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(artifact.Id.ToString()));

        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RuntimeOrchestrationArtifact CreateArtifact(RuntimeOrchestrationLifecycleStatus lifecycle)
    {
        var definitionId = Id.New();
        var versionId = Id.New();
        return new RuntimeOrchestrationArtifact
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version.new",
            SourceOrchestrationVersionId = versionId,
            Version = new SemanticVersion(2, 1, 0),
            ArtifactChecksum = new Checksum("snapshot-checksum"),
            ArtifactPayload = new JsonObject
            {
                ["orchestrationDefinitionId"] = definitionId.ToString(),
                ["orchestrationVersionId"] = versionId.ToString(),
                ["key"] = "sales.sale.created",
                ["version"] = "2.1.0"
            },
            LifecycleStatus = lifecycle,
            Status = lifecycle == RuntimeOrchestrationLifecycleStatus.Archived ? RuntimeOrchestrationArtifactStatus.Retired : RuntimeOrchestrationArtifactStatus.Ready,
            IsActive = lifecycle != RuntimeOrchestrationLifecycleStatus.Archived,
            ReleaseStrategy = "Rollout",
            LastReleaseId = "release-42"
        };
    }
}
