using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class ReleaseApplicationServiceTests
{
    [Fact]
    public async Task CreateBuildsReleaseAndTargetsForEachAllowedRuntimeNode()
    {
        var artifact = Artifact("sales.sale.created");
        var pushNode = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        var pullNode = RuntimeNode(DistributionMode.RuntimeFetchesFromDesign);
        var hybridNode = RuntimeNode(DistributionMode.HybridSync);
        var state = CreateState(artifact, pushNode, pullNode, hybridNode);
        var service = state.CreateService();

        var releaseId = await service.Create(new CreateReleaseInput(
            artifact.Id.ToString(),
            " ",
            "",
            [pushNode.Id.ToString(), pullNode.Id.ToString(), hybridNode.Id.ToString(), pushNode.Id.ToString()],
            "first rollout"));

        var release = Assert.Single(state.CreatedReleases);
        var planTargets = state.CreatedPlanTargets.OrderBy(x => x.RuntimeNodeId.ToString()).ToArray();
        var releaseTargets = state.CreatedReleaseTargets.OrderBy(x => x.RuntimeNodeId.ToString()).ToArray();

        Assert.Equal(release.Id.ToString(), releaseId);
        Assert.Equal(artifact.Id, release.ArtifactId);
        Assert.Equal("sales.sale.created", release.OrchestrationDefinitionId);
        Assert.Equal("web-ui", release.RequestedBy);
        Assert.Equal("Immediate", release.Strategy);
        Assert.Equal(ReleaseStatus.InProgress, release.Status);
        Assert.Equal(3, planTargets.Length);
        Assert.All(planTargets, target =>
        {
            Assert.Equal(release.Id, target.ReleaseId);
            Assert.Equal(ReleaseStatus.InProgress, target.Status);
            Assert.Equal("first rollout", target.Notes);
        });
        Assert.Equal(3, releaseTargets.Length);
        Assert.Equal(ReleaseTargetStatus.PushScheduled, releaseTargets.Single(x => x.RuntimeNodeId == pushNode.Id).Status);
        Assert.Equal(ReleaseTargetStatus.AvailableForPull, releaseTargets.Single(x => x.RuntimeNodeId == pullNode.Id).Status);
        Assert.Equal(ReleaseTargetStatus.AvailableForPull, releaseTargets.Single(x => x.RuntimeNodeId == hybridNode.Id).Status);
        Assert.Null(releaseTargets.Single(x => x.RuntimeNodeId == pushNode.Id).AvailableAtUtc);
        Assert.NotNull(releaseTargets.Single(x => x.RuntimeNodeId == pullNode.Id).AvailableAtUtc);
        Assert.NotNull(releaseTargets.Single(x => x.RuntimeNodeId == hybridNode.Id).AvailableAtUtc);
        Assert.All(releaseTargets, target =>
        {
            Assert.Equal(release.Id, target.ReleaseId);
            Assert.Equal(artifact.Id, target.ArtifactId);
            Assert.Equal("Immediate", target.RolloutGroup);
            Assert.Equal(ActivationStatus.NotActivated, target.ActivationStatus);
            Assert.Equal(release.Id.ToString(), target.CorrelationId);
        });
    }

    [Fact]
    public async Task CreateSkipsAlreadyTargetedNodesAndCreatesOnlyMissingTargets()
    {
        var artifact = Artifact("sales.sale.created");
        var existingNode = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        var missingNode = RuntimeNode(DistributionMode.HybridSync);
        var state = CreateState(artifact, existingNode, missingNode);
        state.ExistingTargets[(artifact.Id, existingNode.Id)] = new ReleaseTarget
        {
            Id = Id.New(),
            RuntimeNodeId = existingNode.Id,
            ArtifactId = artifact.Id,
            Status = ReleaseTargetStatus.Activated
        };
        var service = state.CreateService();

        await service.Create(new CreateReleaseInput(
            artifact.Id.ToString(),
            "tester",
            "canary",
            [existingNode.Id.ToString(), missingNode.Id.ToString()],
            ""));

        Assert.Single(state.CreatedPlanTargets);
        Assert.Single(state.CreatedReleaseTargets);
        Assert.Equal(missingNode.Id, state.CreatedPlanTargets.Single().RuntimeNodeId);
        Assert.Equal(missingNode.Id, state.CreatedReleaseTargets.Single().RuntimeNodeId);
    }

    [Fact]
    public async Task CreateRejectsWhenNoNodePolicyExists()
    {
        var artifact = Artifact("sales.sale.created");
        var node = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        var state = CreateState(artifact, node);
        state.AllowedNodeIds.Clear();
        var service = state.CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Create(new CreateReleaseInput(
            artifact.Id.ToString(),
            "tester",
            "Immediate",
            [node.Id.ToString()],
            "")));

        Assert.Contains("No runtime node policy configured", exception.Message, StringComparison.Ordinal);
        Assert.Empty(state.CreatedReleases);
        Assert.Empty(state.CreatedReleaseTargets);
    }

    [Fact]
    public async Task CreateRejectsRuntimeNodeOutsidePolicy()
    {
        var artifact = Artifact("sales.sale.created");
        var allowedNode = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        var unauthorizedNode = RuntimeNode(DistributionMode.HybridSync);
        var state = CreateState(artifact, allowedNode, unauthorizedNode);
        state.AllowedNodeIds.Clear();
        state.AllowedNodeIds.Add(allowedNode.Id);
        var service = state.CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Create(new CreateReleaseInput(
            artifact.Id.ToString(),
            "tester",
            "Immediate",
            [unauthorizedNode.Id.ToString()],
            "")));

        Assert.Contains("is not allowed", exception.Message, StringComparison.Ordinal);
        Assert.Empty(state.CreatedReleases);
    }

    [Theory]
    [InlineData(RuntimeNodeStatus.Pending, false, false)]
    [InlineData(RuntimeNodeStatus.Suspend, false, false)]
    [InlineData(RuntimeNodeStatus.Enabled, true, true)]
    public async Task CreateRejectsNodesThatCannotReceiveReleases(
        RuntimeNodeStatus status,
        bool isEnabled,
        bool isDeleted)
    {
        var artifact = Artifact("sales.sale.created");
        var node = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        node.Status = status;
        node.IsEnabled = isEnabled;
        node.IsDeleted = isDeleted;
        var state = CreateState(artifact, node);
        var service = state.CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Create(new CreateReleaseInput(
            artifact.Id.ToString(),
            "tester",
            "Immediate",
            [node.Id.ToString()],
            "")));

        Assert.Contains("is not enabled", exception.Message, StringComparison.Ordinal);
        Assert.Empty(state.CreatedReleases);
    }

    [Fact]
    public async Task CreateRejectsWhenEverySelectedNodeAlreadyHasTarget()
    {
        var artifact = Artifact("sales.sale.created");
        var node = RuntimeNode(DistributionMode.HybridSync);
        var state = CreateState(artifact, node);
        state.ExistingTargets[(artifact.Id, node.Id)] = new ReleaseTarget
        {
            Id = Id.New(),
            RuntimeNodeId = node.Id,
            ArtifactId = artifact.Id,
            Status = ReleaseTargetStatus.AvailableForPull
        };
        var service = state.CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Create(new CreateReleaseInput(
            artifact.Id.ToString(),
            "tester",
            "Immediate",
            [node.Id.ToString()],
            "")));

        Assert.Contains("already has a release target", exception.Message, StringComparison.Ordinal);
        Assert.Empty(state.CreatedReleases);
    }

    [Fact]
    public async Task GetAllMapsReleasesAndPlanTargets()
    {
        var artifact = Artifact("sales.sale.created");
        var node = RuntimeNode(DistributionMode.HybridSync);
        var state = CreateState(artifact, node);
        var release = new Release
        {
            Id = Id.New(),
            ArtifactId = artifact.Id,
            OrchestrationDefinitionId = artifact.OrchestrationDefinitionId,
            RequestedBy = "tester",
            Strategy = "Immediate",
            Status = ReleaseStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTime.UtcNow
        };
        var target = new ReleasePlanTarget
        {
            Id = Id.New(),
            ReleaseId = release.Id,
            RuntimeNodeId = node.Id,
            Status = ReleaseStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-4),
            CompletedAtUtc = DateTime.UtcNow,
            Notes = "done"
        };
        state.StoredReleases.Add(release);
        state.StoredPlanTargets[release.Id] = [target];
        var service = state.CreateService();

        var result = await service.GetAll(new ApplicationPagedSettings
        {
            PageNumber = 2,
            PageSize = 25
        });

        var row = Assert.Single(result.Rows);
        var mappedTarget = Assert.Single(row.Targets);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(release.Id.ToString(), row.Id);
        Assert.Equal(artifact.Id.ToString(), row.ArtifactId);
        Assert.Equal("Completed", row.Status);
        Assert.Equal(target.Id.ToString(), mappedTarget.Id);
        Assert.Equal(node.Id.ToString(), mappedTarget.RuntimeNodeId);
        Assert.Equal("Completed", mappedTarget.Status);
        Assert.Equal("done", mappedTarget.Notes);
    }

    private static ReleaseState CreateState(Artifact artifact, params RuntimeNode[] nodes)
        => new(artifact, nodes);

    private static Artifact Artifact(string orchestrationDefinitionId)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionId = orchestrationDefinitionId,
            OrchestrationVersionId = Id.New().ToString(),
            OrchestrationDisplayName = "Sale Created",
            VersionLabel = "1.0.0",
            VersionNumber = "1.0.0",
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0.0",
            Payload = "{}",
            Metadata = "{}",
            SourceEvent = "tests",
            SourceVersion = "1.0.0",
            Checksum = $"checksum-{Guid.NewGuid():N}",
            CreatedAtUtc = DateTime.UtcNow
        };

    private static RuntimeNode RuntimeNode(DistributionMode mode)
        => new()
        {
            Id = Id.New(),
            Name = $"Runtime {Guid.NewGuid():N}",
            Code = $"runtime-{Guid.NewGuid():N}",
            DistributionMode = mode,
            EndpointBaseUri = "https://runtime.local",
            EndpointApiPath = "/runtime/artifacts/deploy",
            Status = RuntimeNodeStatus.Enabled,
            IsEnabled = true,
            IsDeleted = false,
            Description = "Runtime node",
            AccessTokenTtlSeconds = 86400,
            TokenRefreshSkewSeconds = 300,
            TokenValidationCacheTtlSeconds = 300,
            RegisteredAtUtc = DateTime.UtcNow
        };

    private sealed class ReleaseState
    {
        public ReleaseState(Artifact artifact, IReadOnlyCollection<RuntimeNode> nodes)
        {
            Artifact = artifact;
            Nodes = nodes.ToDictionary(x => x.Id);
            AllowedNodeIds.AddRange(nodes.Select(x => x.Id));
        }

        public Artifact Artifact { get; }

        public Dictionary<Id, RuntimeNode> Nodes { get; }

        public List<Id> AllowedNodeIds { get; } = new();

        public Dictionary<(Id ArtifactId, Id RuntimeNodeId), ReleaseTarget> ExistingTargets { get; } = new();

        public List<Release> CreatedReleases { get; } = new();

        public List<ReleasePlanTarget> CreatedPlanTargets { get; } = new();

        public List<ReleaseTarget> CreatedReleaseTargets { get; } = new();

        public List<Release> StoredReleases { get; } = new();

        public Dictionary<Id, IReadOnlyCollection<ReleasePlanTarget>> StoredPlanTargets { get; } = new();

        public ReleaseApplicationService CreateService()
        {
            var releaseRepository = Substitute.For<IReleaseRepository>();
            var releaseTargetRepository = Substitute.For<IReleaseTargetRepository>();
            var artifactRepository = Substitute.For<IArtifactRepository>();
            var runtimeNodeRepository = Substitute.For<IRuntimeNodeRepository>();
            var policyRepository = Substitute.For<IOrchestrationNodePolicyRepository>();

            artifactRepository.GetById(Artifact.Id, Arg.Any<CancellationToken>()).Returns(Artifact);
            policyRepository
                .GetAllowedRuntimeNodeIds(Artifact.OrchestrationDefinitionId, Arg.Any<CancellationToken>())
                .Returns(_ => AllowedNodeIds.ToArray());

            foreach (var node in Nodes.Values)
            {
                runtimeNodeRepository.GetById(node.Id, Arg.Any<CancellationToken>()).Returns(node);
                releaseTargetRepository
                    .GetByArtifactAndRuntimeNode(Artifact.Id, node.Id, Arg.Any<CancellationToken>())
                    .Returns(_ => ExistingTargets.GetValueOrDefault((Artifact.Id, node.Id)));
            }

            releaseRepository
                .Create(Arg.Any<Release>(), Arg.Any<IReadOnlyCollection<ReleasePlanTarget>>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    CreatedReleases.Add(call.ArgAt<Release>(0));
                    CreatedPlanTargets.AddRange(call.ArgAt<IReadOnlyCollection<ReleasePlanTarget>>(1));
                    return Task.CompletedTask;
                });
            releaseTargetRepository
                .Create(Arg.Any<ReleaseTarget>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    CreatedReleaseTargets.Add(call.ArgAt<ReleaseTarget>(0));
                    return Task.CompletedTask;
                });
            releaseRepository
                .GetAll(Arg.Any<PagedSettings>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var settings = call.ArgAt<PagedSettings>(0);
                    return new PagedResult<Release>(
                        settings.PageNumber,
                        StoredReleases.Count == 0 ? 0 : 1,
                        StoredReleases.Count,
                        settings.PageSize,
                        StoredReleases.ToArray());
                });
            releaseRepository
                .GetTargets(Arg.Any<Id>(), Arg.Any<CancellationToken>())
                .Returns(call => StoredPlanTargets.GetValueOrDefault(call.ArgAt<Id>(0), Array.Empty<ReleasePlanTarget>()));

            return new ReleaseApplicationService(
                releaseRepository,
                releaseTargetRepository,
                artifactRepository,
                runtimeNodeRepository,
                policyRepository);
        }
    }
}
