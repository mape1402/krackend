using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class ControlPlaneEntityFrameworkDistributionRepositoryTests
{
    [Fact]
    public async Task DistributionRepositoriesPersistPublishReleaseTargetsAttemptsAndPolicies()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var artifactRepository = services.GetRequiredService<IArtifactRepository>();
        var releaseRepository = services.GetRequiredService<IReleaseRepository>();
        var targetRepository = services.GetRequiredService<IReleaseTargetRepository>();
        var policyRepository = services.GetRequiredService<IOrchestrationNodePolicyRepository>();
        var runtimeNodeId = Id.New();
        var secondRuntimeNodeId = Id.New();
        var orchestrationDefinitionId = "sales.sale.created";
        var orchestrationVersionId = Id.New();
        var olderArtifact = Artifact(orchestrationDefinitionId, orchestrationVersionId, DateTime.UtcNow.AddMinutes(-10));
        var latestArtifact = Artifact(orchestrationDefinitionId, orchestrationVersionId, DateTime.UtcNow);

        await artifactRepository.Create(olderArtifact);
        await artifactRepository.Create(latestArtifact);
        await artifactRepository.SetPublished(latestArtifact.Id, true);

        var persistedArtifact = await artifactRepository.GetById(latestArtifact.Id);
        var latest = await artifactRepository.GetLatestForOrchestrationVersion(orchestrationVersionId, "orchestration-version-snapshot");
        var missingLatest = await artifactRepository.GetLatestForOrchestrationVersion(Id.New(), "orchestration-version-snapshot");
        var pagedArtifacts = await artifactRepository.GetAll(new PagedSettings(1, 10, [], []));

        Assert.True(persistedArtifact.IsPublished);
        Assert.NotNull(persistedArtifact.PublishedAtUtc);
        Assert.Equal(latestArtifact.Id, latest.Id);
        Assert.Null(missingLatest);
        Assert.Equal(2, pagedArtifacts.TotalRows);

        var release = new Release
        {
            Id = Id.New(),
            ArtifactId = latestArtifact.Id,
            OrchestrationDefinitionId = orchestrationDefinitionId,
            RequestedBy = "operator",
            Strategy = "hybrid",
            Status = ReleaseStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };
        var targets = new[]
        {
            ReleasePlanTarget(release.Id, runtimeNodeId, "primary"),
            ReleasePlanTarget(release.Id, secondRuntimeNodeId, "secondary")
        };

        await releaseRepository.Create(release, targets);
        await releaseRepository.ApplyTargetStatus(release.Id, runtimeNodeId, ReleaseStatus.Completed);

        var inProgressRelease = await releaseRepository.GetById(release.Id);
        var persistedTargets = await releaseRepository.GetTargets(release.Id);

        Assert.Equal(ReleaseStatus.InProgress, inProgressRelease.Status);
        Assert.Null(inProgressRelease.CompletedAtUtc);
        Assert.Equal(2, persistedTargets.Count);
        Assert.Contains(persistedTargets, x => x.RuntimeNodeId == runtimeNodeId && x.Status == ReleaseStatus.Completed);

        await releaseRepository.ApplyTargetStatus(release.Id, secondRuntimeNodeId, ReleaseStatus.Failed);
        var failedRelease = await releaseRepository.GetById(release.Id);
        Assert.Equal(ReleaseStatus.Failed, failedRelease.Status);
        Assert.NotNull(failedRelease.CompletedAtUtc);

        var singleTargetRelease = new Release
        {
            Id = Id.New(),
            ArtifactId = latestArtifact.Id,
            OrchestrationDefinitionId = orchestrationDefinitionId,
            RequestedBy = "operator",
            Strategy = "push",
            Status = ReleaseStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(1)
        };
        await releaseRepository.Create(singleTargetRelease, [ReleasePlanTarget(singleTargetRelease.Id, runtimeNodeId, "single")]);
        await releaseRepository.ApplyTargetStatus(singleTargetRelease.Id, runtimeNodeId, ReleaseStatus.Completed);
        Assert.Equal(ReleaseStatus.Completed, (await releaseRepository.GetById(singleTargetRelease.Id)).Status);
        Assert.Equal(2, (await releaseRepository.GetAll(new PagedSettings(1, 10, [], []))).TotalRows);

        var releaseTarget = new ReleaseTarget
        {
            Id = Id.New(),
            RuntimeNodeId = runtimeNodeId,
            ArtifactId = latestArtifact.Id,
            ReleaseId = release.Id,
            RolloutGroup = "primary",
            Status = ReleaseTargetStatus.Pending,
            ActivationStatus = ActivationStatus.NotActivated,
            AssignedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            CorrelationId = "corr-1"
        };
        var newerReleaseTarget = new ReleaseTarget
        {
            Id = Id.New(),
            RuntimeNodeId = runtimeNodeId,
            ArtifactId = latestArtifact.Id,
            ReleaseId = singleTargetRelease.Id,
            RolloutGroup = "primary",
            Status = ReleaseTargetStatus.AvailableForPull,
            ActivationStatus = ActivationStatus.NotActivated,
            AssignedAtUtc = DateTime.UtcNow,
            CorrelationId = "corr-2"
        };
        var deliveredTarget = new ReleaseTarget
        {
            Id = Id.New(),
            RuntimeNodeId = secondRuntimeNodeId,
            ArtifactId = olderArtifact.Id,
            ReleaseId = release.Id,
            RolloutGroup = "secondary",
            Status = ReleaseTargetStatus.Delivered,
            ActivationStatus = ActivationStatus.Activating,
            AssignedAtUtc = DateTime.UtcNow,
            DeliveredAtUtc = DateTime.UtcNow,
            CorrelationId = "corr-3"
        };

        await targetRepository.Create(releaseTarget);
        await targetRepository.Create(newerReleaseTarget);
        await targetRepository.Create(deliveredTarget);

        releaseTarget.Status = ReleaseTargetStatus.Failed;
        releaseTarget.ActivationStatus = ActivationStatus.ActivationFailed;
        releaseTarget.FailedAtUtc = DateTime.UtcNow;
        releaseTarget.FailureReason = "boom";
        releaseTarget.RuntimeVersionApplied = "1.0.0";
        await targetRepository.Update(releaseTarget);
        await targetRepository.AddAttempt(new ReleaseAttempt
        {
            Id = Id.New(),
            ReleaseTargetId = releaseTarget.Id,
            Action = "push",
            InitiatedBy = "operator",
            StartedAtUtc = DateTime.UtcNow.AddSeconds(-5),
            FinishedAtUtc = DateTime.UtcNow,
            Succeeded = false,
            ErrorCode = "RuntimeRejected",
            ErrorMessage = "Rejected",
            ExternalReference = "ext-1"
        });

        var persistedReleaseTarget = await targetRepository.GetById(releaseTarget.Id);
        var latestForArtifactRuntime = await targetRepository.GetByArtifactAndRuntimeNode(latestArtifact.Id, runtimeNodeId);
        var attempts = await targetRepository.GetAttempts(releaseTarget.Id);
        var pendingForRuntime = await targetRepository.GetPendingForRuntimeNode(runtimeNodeId);
        var allTargets = await targetRepository.GetAll(new PagedSettings(1, 10, [], []));

        Assert.Equal(ReleaseTargetStatus.Failed, persistedReleaseTarget.Status);
        Assert.Equal("boom", persistedReleaseTarget.FailureReason);
        Assert.Equal(newerReleaseTarget.Id, latestForArtifactRuntime.Id);
        Assert.Single(attempts);
        Assert.Equal("RuntimeRejected", attempts.Single().ErrorCode);
        Assert.Contains(pendingForRuntime, x => x.Id == newerReleaseTarget.Id);
        Assert.DoesNotContain(pendingForRuntime, x => x.Id == deliveredTarget.Id);
        Assert.Equal(3, allTargets.TotalRows);

        await policyRepository.Replace(orchestrationDefinitionId,
        [
            new OrchestrationAllowedRuntimeNode
            {
                Id = Id.New(),
                OrchestrationDefinitionId = orchestrationDefinitionId,
                RuntimeNodeId = runtimeNodeId,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "operator"
            },
            new OrchestrationAllowedRuntimeNode
            {
                Id = Id.New(),
                OrchestrationDefinitionId = orchestrationDefinitionId,
                RuntimeNodeId = secondRuntimeNodeId,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "operator"
            }
        ]);

        var allowed = await policyRepository.GetAllowedRuntimeNodeIds(orchestrationDefinitionId);
        var byOrchestration = await policyRepository.GetAllowedRuntimeNodeIdsByOrchestrationIds([orchestrationDefinitionId, "", orchestrationDefinitionId]);
        var emptyByOrchestration = await policyRepository.GetAllowedRuntimeNodeIdsByOrchestrationIds([]);

        Assert.Equal(2, allowed.Count);
        Assert.Equal(2, byOrchestration[orchestrationDefinitionId].Count);
        Assert.Empty(emptyByOrchestration);

        await policyRepository.Replace(orchestrationDefinitionId, []);
        Assert.Empty(await policyRepository.GetAllowedRuntimeNodeIds(orchestrationDefinitionId));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<Sieve.Models.SieveOptions>(_ => { });
        services.AddOrchestratorControlPlaneStorageEntityFramework(options =>
            options.UseInMemoryDatabase($"control-plane-distribution-{Guid.NewGuid():N}"));
        return services.BuildServiceProvider();
    }

    private static Artifact Artifact(string orchestrationDefinitionId, Id orchestrationVersionId, DateTime createdAtUtc)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionId = orchestrationDefinitionId,
            OrchestrationVersionId = orchestrationVersionId.ToString(),
            OrchestrationDisplayName = "Sale Created",
            VersionLabel = "1.0.0",
            VersionNumber = "1.0.0",
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0.0",
            Payload = """{"definition":"payload"}""",
            Metadata = """{"metadata":true}""",
            SourceEvent = "OrchestrationVersionDeployed",
            SourceVersion = "1.0.0",
            Checksum = $"sha256:{createdAtUtc.Ticks}",
            CreatedAtUtc = createdAtUtc
        };

    private static ReleasePlanTarget ReleasePlanTarget(Id releaseId, Id runtimeNodeId, string notes)
        => new()
        {
            Id = Id.New(),
            ReleaseId = releaseId,
            RuntimeNodeId = runtimeNodeId,
            Status = ReleaseStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            Notes = notes
        };
}
