using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class ArtifactPublicationApplicationServiceTests
{
    [Fact]
    public async Task PublishDeploymentCreatesValidatedArtifactWithLifecycleMetadata()
    {
        var repository = new RecordingArtifactRepository();
        var service = CreateService(repository);
        var occurredAtUtc = DateTime.UtcNow.AddMinutes(-7);
        var integrationEvent = DeploymentEvent(occurredAtUtc: occurredAtUtc);

        await service.PublishDeployment(integrationEvent);

        var artifact = Assert.Single(repository.CreatedArtifacts);
        Assert.Equal(integrationEvent.OrchestrationVersionId, artifact.OrchestrationVersionId);
        Assert.Equal(integrationEvent.OrchestrationDefinitionId, artifact.OrchestrationDefinitionId);
        Assert.Equal(integrationEvent.OrchestrationDisplayName, artifact.OrchestrationDisplayName);
        Assert.Equal(integrationEvent.VersionLabel, artifact.VersionLabel);
        Assert.Equal(integrationEvent.VersionNumber, artifact.VersionNumber);
        Assert.Equal("orchestration-version-snapshot", artifact.ArtifactType);
        Assert.Equal("1.0", artifact.SchemaVersion);
        Assert.Equal(integrationEvent.ArtifactPayloadJson, artifact.Payload);
        Assert.Equal(integrationEvent.Checksum, artifact.Checksum);
        Assert.False(artifact.IsPublished);
        Assert.Null(artifact.PublishedAtUtc);
        Assert.Equal(nameof(OrchestrationVersionDeployedEvent), artifact.SourceEvent);
        Assert.Equal(integrationEvent.VersionNumber, artifact.SourceVersion);

        using var metadata = JsonDocument.Parse(artifact.Metadata);
        Assert.Equal(nameof(OrchestrationVersionDeployedEvent), metadata.RootElement.GetProperty("sourceEvent").GetString());
        Assert.Equal(occurredAtUtc, metadata.RootElement.GetProperty("occurredAtUtc").GetDateTime());
    }

    [Fact]
    public async Task PublishDeploymentIsIdempotentWhenLatestArtifactHasSameChecksum()
    {
        var repository = new RecordingArtifactRepository();
        var service = CreateService(repository);
        var integrationEvent = DeploymentEvent(checksum: "same-checksum");
        var existing = ArtifactFor(integrationEvent, checksum: "same-checksum");
        repository.LatestArtifacts[(integrationEvent.OrchestrationVersionId, "orchestration-version-snapshot")] = existing;

        await service.PublishDeployment(integrationEvent);

        Assert.Empty(repository.CreatedArtifacts);
    }

    [Fact]
    public async Task PublishDeploymentCreatesNewArtifactWhenLatestChecksumDiffers()
    {
        var repository = new RecordingArtifactRepository();
        var service = CreateService(repository);
        var integrationEvent = DeploymentEvent(checksum: "new-checksum");
        repository.LatestArtifacts[(integrationEvent.OrchestrationVersionId, "orchestration-version-snapshot")] = ArtifactFor(
            integrationEvent,
            checksum: "old-checksum");

        await service.PublishDeployment(integrationEvent);

        var artifact = Assert.Single(repository.CreatedArtifacts);
        Assert.Equal("new-checksum", artifact.Checksum);
    }

    [Theory]
    [InlineData("deprecated")]
    [InlineData("archived")]
    public async Task PublishLifecycleChangesCreatesArtifactFromMatchingEvent(string lifecycle)
    {
        var repository = new RecordingArtifactRepository();
        var service = CreateService(repository);

        if (lifecycle == "deprecated")
        {
            await service.PublishDeprecation(DeprecationEvent());
        }
        else
        {
            await service.PublishArchive(ArchiveEvent());
        }

        var artifact = Assert.Single(repository.CreatedArtifacts);
        Assert.Equal(
            lifecycle == "deprecated"
                ? nameof(OrchestrationVersionDeprecatedEvent)
                : nameof(OrchestrationVersionArchivedEvent),
            artifact.SourceEvent);
        Assert.Equal("""{"Key":"sales.sale.created"}""", artifact.Payload);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    public async Task PublishDeploymentRejectsInvalidJsonPayloads(string payload)
    {
        var repository = new RecordingArtifactRepository();
        var service = CreateService(repository);
        var integrationEvent = DeploymentEvent(payload: payload);

        await Assert.ThrowsAnyAsync<Exception>(() => service.PublishDeployment(integrationEvent));

        Assert.Empty(repository.CreatedArtifacts);
    }

    [Fact]
    public void BuildersRejectNullEvents()
    {
        var factory = new ArtifactFactory();

        Assert.Throws<ArgumentNullException>(() => new DeployedArtifactBuilder(factory).Build(null!));
        Assert.Throws<ArgumentNullException>(() => new DeprecatedArtifactBuilder(factory).Build(null!));
        Assert.Throws<ArgumentNullException>(() => new ArchivedArtifactBuilder(factory).Build(null!));
    }

    private static ArtifactPublicationApplicationService CreateService(IArtifactRepository repository)
    {
        var factory = new ArtifactFactory();
        return new ArtifactPublicationApplicationService(
            new DeployedArtifactBuilder(factory),
            new DeprecatedArtifactBuilder(factory),
            new ArchivedArtifactBuilder(factory),
            [new JsonArtifactValidationPolicy()],
            repository);
    }

    private static OrchestrationVersionDeployedEvent DeploymentEvent(
        string? versionId = null,
        string payload = """{"Key":"sales.sale.created"}""",
        string checksum = "checksum",
        DateTime? occurredAtUtc = null)
        => new(
            versionId ?? Id.New().ToString(),
            "sales.sale.created",
            "Sale Created",
            "1.0.0",
            "1.0.0",
            payload,
            checksum,
            "tester",
            "correlation-1",
            occurredAtUtc ?? DateTime.UtcNow);

    private static OrchestrationVersionDeprecatedEvent DeprecationEvent()
        => new(
            Id.New().ToString(),
            "sales.sale.created",
            "Sale Created",
            "1.0.0",
            "1.0.0",
            """{"Key":"sales.sale.created"}""",
            "checksum",
            "tester",
            "correlation-1",
            DateTime.UtcNow);

    private static OrchestrationVersionArchivedEvent ArchiveEvent()
        => new(
            Id.New().ToString(),
            "sales.sale.created",
            "Sale Created",
            "1.0.0",
            "1.0.0",
            """{"Key":"sales.sale.created"}""",
            "checksum",
            "tester",
            "correlation-1",
            DateTime.UtcNow);

    private static Artifact ArtifactFor(OrchestrationVersionDeployedEvent integrationEvent, string checksum)
        => new()
        {
            Id = Id.New(),
            OrchestrationVersionId = integrationEvent.OrchestrationVersionId,
            OrchestrationDefinitionId = integrationEvent.OrchestrationDefinitionId,
            OrchestrationDisplayName = integrationEvent.OrchestrationDisplayName,
            VersionLabel = integrationEvent.VersionLabel,
            VersionNumber = integrationEvent.VersionNumber,
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0",
            Payload = integrationEvent.ArtifactPayloadJson,
            Metadata = "{}",
            SourceEvent = nameof(OrchestrationVersionDeployedEvent),
            SourceVersion = integrationEvent.VersionNumber,
            Checksum = checksum,
            IsPublished = false,
            CreatedAtUtc = DateTime.UtcNow
        };

    private sealed class RecordingArtifactRepository : IArtifactRepository
    {
        public Dictionary<(string OrchestrationVersionId, string ArtifactType), Artifact> LatestArtifacts { get; } = new();

        public List<Artifact> CreatedArtifacts { get; } = new();

        public Task Create(Artifact artifact, CancellationToken cancellationToken = default)
        {
            CreatedArtifacts.Add(artifact);
            LatestArtifacts[(artifact.OrchestrationVersionId, artifact.ArtifactType)] = artifact;
            return Task.CompletedTask;
        }

        public Task SetPublished(Id artifactId, bool isPublished, CancellationToken cancellationToken = default)
        {
            var artifact = CreatedArtifacts.SingleOrDefault(x => x.Id == artifactId);
            if (artifact is not null)
            {
                artifact.IsPublished = isPublished;
            }

            return Task.CompletedTask;
        }

        public Task<Artifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
            => Task.FromResult(CreatedArtifacts.Single(x => x.Id == artifactId));

        public Task<Artifact> GetLatestForOrchestrationVersion(
            Id orchestrationVersionId,
            string artifactType,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LatestArtifacts.GetValueOrDefault((orchestrationVersionId.ToString(), artifactType))!);

        public Task<PagedResult<Artifact>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<Artifact>(
                pagedSettings.PageNumber,
                1,
                CreatedArtifacts.Count,
                pagedSettings.PageSize,
                CreatedArtifacts));
    }
}
