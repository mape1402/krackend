using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class DistributionReadModelApplicationServiceTests
{
    [Fact]
    public async Task ArtifactServiceMapsPagedArtifactsWithoutDroppingLifecycleFields()
    {
        var repository = Substitute.For<IArtifactRepository>();
        var created = DateTime.UtcNow.AddMinutes(-10);
        var published = DateTime.UtcNow.AddMinutes(-5);
        var artifact = new Artifact
        {
            Id = Id.New(),
            OrchestrationDefinitionId = "sales.sale.created",
            OrchestrationVersionId = "version-1",
            OrchestrationDisplayName = "Sale Created",
            VersionLabel = "v1",
            VersionNumber = "1.0.0",
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0",
            Payload = """{"Key":"sales.sale.created"}""",
            Metadata = """{"source":"test"}""",
            SourceEvent = "OrchestrationVersionDeployed",
            SourceVersion = "1.0.0",
            Checksum = "checksum",
            IsPublished = true,
            CreatedAtUtc = created,
            PublishedAtUtc = published
        };
        repository.GetAll(Arg.Any<PagedSettings>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var settings = call.ArgAt<PagedSettings>(0);
                Assert.Equal(3, settings.PageNumber);
                Assert.Equal(25, settings.PageSize);
                Assert.Empty(settings.Filters);
                Assert.Empty(settings.Sorts);
                return new PagedResult<Artifact>(3, 9, 222, 25, [artifact]);
            });
        var service = new ArtifactApplicationService(repository);

        var result = await service.GetAll(new ApplicationPagedSettings { PageNumber = 3, PageSize = 25 });

        var row = Assert.Single(result.Rows);
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(222, result.TotalRows);
        Assert.Equal(9, result.TotalPages);
        Assert.Equal(artifact.Id.ToString(), row.Id);
        Assert.Equal("sales.sale.created", row.OrchestrationDefinitionId);
        Assert.Equal("version-1", row.OrchestrationVersionId);
        Assert.Equal("Sale Created", row.OrchestrationDisplayName);
        Assert.Equal("v1", row.VersionLabel);
        Assert.Equal("1.0.0", row.VersionNumber);
        Assert.Equal("orchestration-version-snapshot", row.ArtifactType);
        Assert.Equal("1.0", row.SchemaVersion);
        Assert.Equal(artifact.Payload, row.Payload);
        Assert.Equal(artifact.Metadata, row.Metadata);
        Assert.Equal("OrchestrationVersionDeployed", row.SourceEvent);
        Assert.Equal("checksum", row.Checksum);
        Assert.True(row.IsPublished);
        Assert.Equal(created, row.CreatedAtUtc);
        Assert.Equal(published, row.PublishedAtUtc);
    }

    [Fact]
    public async Task ReleaseTargetServiceMapsTargetsAndAttempts()
    {
        var repository = Substitute.For<IReleaseTargetRepository>();
        var target = new ReleaseTarget
        {
            Id = Id.New(),
            RuntimeNodeId = Id.New(),
            ArtifactId = Id.New(),
            ReleaseId = Id.New(),
            RolloutGroup = "default",
            Status = ReleaseTargetStatus.Delivered,
            ActivationStatus = ActivationStatus.Activating,
            AssignedAtUtc = DateTime.UtcNow.AddMinutes(-7),
            CorrelationId = "corr-1"
        };
        var attempt = new ReleaseAttempt
        {
            Id = Id.New(),
            ReleaseTargetId = target.Id,
            Action = "Push",
            InitiatedBy = "tester",
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-6),
            FinishedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Succeeded = false,
            ErrorCode = "RuntimeUnavailable",
            ErrorMessage = "runtime is offline",
            ExternalReference = "request-1"
        };
        repository.GetAll(Arg.Any<PagedSettings>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var settings = call.ArgAt<PagedSettings>(0);
                Assert.Equal(2, settings.PageNumber);
                Assert.Equal(10, settings.PageSize);
                return new PagedResult<ReleaseTarget>(2, 5, 42, 10, [target]);
            });
        repository.GetAttempts(target.Id, Arg.Any<CancellationToken>())
            .Returns([attempt]);
        var service = new ReleaseTargetApplicationService(repository);

        var targets = await service.GetAll(new ApplicationPagedSettings { PageNumber = 2, PageSize = 10 });
        var attempts = await service.GetAttempts(target.Id.ToString());

        var row = Assert.Single(targets.Rows);
        Assert.Equal(target.Id.ToString(), row.Id);
        Assert.Equal(target.RuntimeNodeId.ToString(), row.RuntimeNodeId);
        Assert.Equal(target.ArtifactId.ToString(), row.ArtifactId);
        Assert.Equal(target.ReleaseId.Value.ToString(), row.ReleaseId);
        Assert.Equal("default", row.RolloutGroup);
        Assert.Equal("Delivered", row.Status);
        Assert.Equal("Activating", row.ActivationStatus);
        Assert.Equal(target.AssignedAtUtc, row.AssignedAtUtc);

        var attemptRow = Assert.Single(attempts);
        Assert.Equal(attempt.Id.ToString(), attemptRow.Id);
        Assert.Equal(target.Id.ToString(), attemptRow.ReleaseTargetId);
        Assert.Equal("Push", attemptRow.Action);
        Assert.Equal("tester", attemptRow.InitiatedBy);
        Assert.False(attemptRow.Succeeded);
        Assert.Equal("RuntimeUnavailable", attemptRow.ErrorCode);
        Assert.Equal("runtime is offline", attemptRow.ErrorMessage);
        Assert.Equal("request-1", attemptRow.ExternalReference);
    }
}
