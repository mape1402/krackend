namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

public sealed class RuntimeArtifactDeploymentServiceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemanticVersion Version = new(1, 0, 0);

    [Fact]
    public async Task DeployAsync_WhenExistingReadyArtifactHasSameChecksum_ReturnsReadyWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var existing = repository.Add(CreateRuntimeArtifact(
            "sales.sale.created",
            Version,
            "checksum-a",
            RuntimeOrchestrationArtifactStatus.Ready,
            ingressGeneration: 4));
        var package = CreatePackage(
            existing.OrchestrationDefinitionKey,
            existing.Version,
            existing.ArtifactChecksum.Value,
            existing.SourceOrchestrationVersionId);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.True(result.Accepted, result.Message);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready.ToString(), result.Status);
        Assert.Equal(existing.Id.ToString(), result.RuntimeArtifactId);
        Assert.Empty(scheduler.Requests);
        Assert.Equal(0, repository.UpsertCount);
    }

    [Fact]
    public async Task DeployAsync_WhenExistingFailedArtifactHasSameChecksum_RequeuesProjectionWithSameGeneration()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var existing = repository.Add(CreateRuntimeArtifact(
            "sales.sale.created",
            Version,
            "checksum-a",
            RuntimeOrchestrationArtifactStatus.Failed,
            ingressGeneration: 6));
        existing.ProjectionError = "Previous projection failed.";
        var package = CreatePackage(
            existing.OrchestrationDefinitionKey,
            existing.Version,
            existing.ArtifactChecksum.Value,
            existing.SourceOrchestrationVersionId);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.True(result.Accepted, result.Message);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Pending.ToString(), result.Status);
        Assert.Single(scheduler.Requests);
        Assert.Equal(existing.Id.ToString(), scheduler.Requests[0].ArtifactId);
        Assert.Equal(existing.IngressGeneration, scheduler.Requests[0].IngressGeneration);
        Assert.Equal(1, repository.UpsertCount);
    }

    [Fact]
    public async Task DeployAsync_WhenExistingVersionHasDifferentChecksum_RejectsWithoutChangingRuntimeState()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var existing = repository.Add(CreateRuntimeArtifact(
            "sales.sale.created",
            Version,
            "checksum-a",
            RuntimeOrchestrationArtifactStatus.Ready,
            ingressGeneration: 2));
        var package = CreatePackage(
            existing.OrchestrationDefinitionKey,
            existing.Version,
            "checksum-b",
            existing.SourceOrchestrationVersionId);
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("different checksum", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Equal(0, repository.UpsertCount);
        Assert.Equal("checksum-a", repository.Artifacts.Single().ArtifactChecksum.Value);
    }

    [Fact]
    public async Task DeployAsync_WhenPackageChecksumDoesNotMatchPayloadChecksum_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var versionId = Id.New();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "package-checksum",
            versionId,
            payloadChecksum: "payload-checksum");
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("checksum", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenPackagePayloadHasDifferentOrchestrationKey_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var versionId = Id.New();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            versionId,
            payloadKey: "payments.payment.captured");
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("key", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    [Fact]
    public async Task DeployAsync_WhenPackagePayloadIsInvalidJson_RejectsWithoutSchedulingProjection()
    {
        var repository = new TestRuntimeArtifactDeploymentRepository();
        var scheduler = new RecordingRuntimeArtifactProjectionScheduler();
        var package = CreatePackage(
            "sales.sale.created",
            Version,
            "checksum-a",
            Id.New());
        package.PayloadJson = "{";
        var service = CreateService(repository, scheduler);

        var result = await service.DeployAsync(package, "design-a");

        Assert.False(result.Accepted);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("invalid", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(scheduler.Requests);
        Assert.Empty(repository.Artifacts);
    }

    private static RuntimeArtifactDeploymentService CreateService(
        TestRuntimeArtifactDeploymentRepository repository,
        RecordingRuntimeArtifactProjectionScheduler scheduler)
        => new(repository, new NoopRuntimeStorageUnitOfWork(), scheduler);

    private static RuntimeOrchestrationArtifact CreateRuntimeArtifact(
        string key,
        SemanticVersion version,
        string checksum,
        RuntimeOrchestrationArtifactStatus status,
        long ingressGeneration)
    {
        var versionId = Id.New();
        return new RuntimeOrchestrationArtifact
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = key,
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = versionId,
            Version = version,
            ArtifactChecksum = new Checksum(checksum),
            ArtifactPayload = JsonSerializer.SerializeToNode(CreateArtifact(key, version, checksum, versionId), SerializerOptions)!,
            Status = status,
            IngressGeneration = ingressGeneration,
            IsActive = true,
            LoadedToCache = false,
            DeployedOnUtc = DateTime.UtcNow
        };
    }

    private static RuntimeArtifactDeliveryPackage CreatePackage(
        string key,
        SemanticVersion version,
        string checksum,
        Id versionId,
        string payloadChecksum = "",
        string payloadKey = "")
        => new()
        {
            ReleaseTargetId = Id.New().ToString(),
            ArtifactId = Id.New().ToString(),
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0",
            OrchestrationDefinitionId = Id.New().ToString(),
            OrchestrationVersionId = versionId.ToString(),
            OrchestrationDefinitionKey = key,
            Version = version.ToString(),
            Checksum = checksum,
            PayloadJson = JsonSerializer.Serialize(
                CreateArtifact(
                    string.IsNullOrWhiteSpace(payloadKey) ? key : payloadKey,
                    version,
                    string.IsNullOrWhiteSpace(payloadChecksum) ? checksum : payloadChecksum,
                    versionId),
                SerializerOptions),
            CorrelationId = Id.New().ToString(),
            PromotedBy = "tests",
            PromotedOnUtc = DateTime.UtcNow
        };

    private static OrchestrationArtifact CreateArtifact(
        string key,
        SemanticVersion version,
        string checksum,
        Id versionId)
        => new(
            Id.New(),
            versionId,
            key,
            key,
            "sales",
            version,
            new Checksum(checksum),
            [],
            [],
            []);
}
