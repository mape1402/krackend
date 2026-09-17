using System.Net;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class ArtifactDeliveryApplicationServiceTests
{
    [Fact]
    public async Task PushMakesPullOnlyTargetsAvailableWithoutCallingRuntime()
    {
        var state = CreateState(DistributionMode.RuntimeFetchesFromDesign);
        var service = CreateService(state);

        var result = await service.Push(state.Target.Id.ToString(), "tester");

        Assert.True(result.Succeeded);
        Assert.Equal("AvailableForPull", result.Status);
        Assert.Equal(ReleaseTargetStatus.AvailableForPull, state.Target.Status);
        Assert.NotNull(state.Target.AvailableAtUtc);
        Assert.Empty(state.Attempts);
    }

    [Fact]
    public async Task PushMarksAlreadyActivatedTargetsAsPublished()
    {
        var state = CreateState(DistributionMode.DesignPublishesToRuntime);
        state.Target.Status = ReleaseTargetStatus.Activated;
        state.Target.ActivationStatus = ActivationStatus.Activated;
        state.Target.RuntimeVersionApplied = "runtime-artifact-1";
        var service = CreateService(state);

        var result = await service.Push(state.Target.Id.ToString(), "tester");

        Assert.True(result.Succeeded);
        Assert.Equal("Activated", result.Status);
        Assert.True(state.Artifact.IsPublished);
        Assert.Equal(ReleaseStatus.Completed, state.ReleaseStatus);
    }

    [Fact]
    public async Task PushSendsAuthenticatedPackageAndMarksReadyRuntimeAsActivated()
    {
        var state = CreateState(DistributionMode.DesignPublishesToRuntime);
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(new RuntimeArtifactDeploymentResult
        {
            Accepted = true,
            RuntimeArtifactId = "runtime-artifact-1",
            Status = "Ready",
            Message = "ready"
        })));
        var service = CreateService(state, handler);

        var result = await service.Push(state.Target.Id.ToString(), "tester");

        Assert.True(result.Succeeded);
        Assert.Equal("Activated", result.Status);
        Assert.Equal("runtime-artifact-1", result.ExternalReference);
        Assert.Equal(new Uri("https://runtime.local/runtime/artifacts/deploy"), handler.RequestUri);
        Assert.Equal("Bearer", handler.LastAuthorizationScheme);
        Assert.Equal(ReleaseTargetStatus.Activated, state.Target.Status);
        Assert.Equal(ActivationStatus.Activated, state.Target.ActivationStatus);
        Assert.True(state.Artifact.IsPublished);
        Assert.Single(state.Attempts);
        Assert.True(state.Attempts.Single().Succeeded);
    }

    [Fact]
    public async Task PushMarksAcceptedRuntimeAsDeliveredWhileActivationContinues()
    {
        var state = CreateState(DistributionMode.DesignPublishesToRuntime);
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(new RuntimeArtifactDeploymentResult
        {
            Accepted = true,
            RuntimeArtifactId = "runtime-artifact-1",
            Status = "Pending",
            Message = "accepted"
        })));
        var service = CreateService(state, handler);

        var result = await service.Push(state.Target.Id.ToString(), "tester");

        Assert.True(result.Succeeded);
        Assert.Equal("Delivered", result.Status);
        Assert.Equal(ReleaseTargetStatus.Delivered, state.Target.Status);
        Assert.Equal(ActivationStatus.Activating, state.Target.ActivationStatus);
        Assert.Null(state.Target.ActivatedAtUtc);
        Assert.False(state.Artifact.IsPublished);
    }

    [Fact]
    public async Task PushFallsBackToPullForHybridNodesWhenRuntimeRejectsPackage()
    {
        var state = CreateState(DistributionMode.HybridSync);
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("Runtime rejected", HttpStatusCode.BadRequest));
        var service = CreateService(state, handler);

        var result = await service.Push(state.Target.Id.ToString(), "tester");

        Assert.False(result.Succeeded);
        Assert.Equal("AvailableForPull", result.Status);
        Assert.Equal(ReleaseTargetStatus.AvailableForPull, state.Target.Status);
        Assert.Equal(ActivationStatus.ActivationFailed, state.Target.ActivationStatus);
        Assert.Contains("Runtime rejected", state.Target.FailureReason, StringComparison.Ordinal);
        Assert.Single(state.Attempts);
        Assert.False(state.Attempts.Single().Succeeded);
    }

    [Fact]
    public async Task PushMarksPushOnlyTargetFailedWhenRuntimeCannotAcceptPackage()
    {
        var state = CreateState(DistributionMode.DesignPublishesToRuntime);
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("""{"accepted":false,"message":"not ready"}"""));
        var service = CreateService(state, handler);

        var result = await service.Push(state.Target.Id.ToString(), "tester");

        Assert.False(result.Succeeded);
        Assert.Equal("Failed", result.Status);
        Assert.Equal(ReleaseTargetStatus.Failed, state.Target.Status);
        Assert.Equal(ReleaseStatus.Failed, state.ReleaseStatus);
        Assert.Contains("not ready", state.Target.FailureReason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ConnectionCredentialStatus.Missing, "https://runtime.local", "outbound credential")]
    [InlineData(ConnectionCredentialStatus.Active, "", "endpoint base URI")]
    public async Task PushMarksPushOnlyTargetFailedWhenRuntimeNodeCannotBeCalled(
        ConnectionCredentialStatus outboundStatus,
        string endpoint,
        string expectedMessage)
    {
        var state = CreateState(DistributionMode.DesignPublishesToRuntime);
        state.Node.OutboundCredentialStatus = outboundStatus;
        state.Node.EndpointBaseUri = endpoint;
        var service = CreateService(state);

        var result = await service.Push(state.Target.Id.ToString(), "tester");

        Assert.False(result.Succeeded);
        Assert.Equal("Failed", result.Status);
        Assert.Equal(ReleaseTargetStatus.Failed, state.Target.Status);
        Assert.Equal(ReleaseStatus.Failed, state.ReleaseStatus);
        Assert.Contains(expectedMessage, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(state.Attempts);
        Assert.False(state.Attempts.Single().Succeeded);
    }

    [Theory]
    [InlineData(RuntimeNodeStatus.Pending, false, "not enabled")]
    [InlineData(RuntimeNodeStatus.Enabled, true, "was deleted")]
    public async Task PushMarksTargetFailedWhenRuntimeNodeCannotDistribute(
        RuntimeNodeStatus status,
        bool isDeleted,
        string expectedMessage)
    {
        var state = CreateState(DistributionMode.DesignPublishesToRuntime);
        state.Node.Status = status;
        state.Node.IsDeleted = isDeleted;
        var service = CreateService(state);

        var result = await service.Push(state.Target.Id.ToString(), "tester");

        Assert.False(result.Succeeded);
        Assert.Equal("Failed", result.Status);
        Assert.Contains(expectedMessage, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ReleaseStatus.Failed, state.ReleaseStatus);
    }

    [Fact]
    public async Task PullQueriesPromotePendingTargetsAndRejectInvalidTargets()
    {
        var state = CreateState(DistributionMode.HybridSync);
        var service = CreateService(state);

        var pending = await service.GetPendingForPull(state.Node.Id.ToString());
        var package = await service.GetForPull(state.Node.Id.ToString(), state.Target.Id.ToString());

        Assert.Single(pending);
        Assert.Equal(state.Target.Id.ToString(), package.ReleaseTargetId);
        Assert.Equal(ReleaseTargetStatus.AvailableForPull, state.Target.Status);

        var otherNode = RuntimeNode(DistributionMode.HybridSync);
        state.Nodes[otherNode.Id] = otherNode;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetForPull(otherNode.Id.ToString(), state.Target.Id.ToString()));

        state.Node.DistributionMode = DistributionMode.DesignPublishesToRuntime;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetPendingForPull(state.Node.Id.ToString()));
    }

    [Fact]
    public async Task GetForPullPromotesPendingTargetBeforeBuildingPackage()
    {
        var state = CreateState(DistributionMode.RuntimeFetchesFromDesign);
        var service = CreateService(state);

        var package = await service.GetForPull(state.Node.Id.ToString(), state.Target.Id.ToString());

        Assert.Equal(state.Target.Id.ToString(), package.ReleaseTargetId);
        Assert.Equal(ReleaseTargetStatus.AvailableForPull, state.Target.Status);
        Assert.NotNull(state.Target.AvailableAtUtc);
    }

    [Fact]
    public async Task GetForPullRejectsTargetsThatAreNotAvailableForPull()
    {
        var state = CreateState(DistributionMode.RuntimeFetchesFromDesign);
        state.Target.Status = ReleaseTargetStatus.Delivered;
        var service = CreateService(state);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetForPull(state.Node.Id.ToString(), state.Target.Id.ToString()));

        Assert.Equal("Release target is not available for pull.", exception.Message);
    }

    [Theory]
    [InlineData("Ready", ReleaseTargetStatus.Activated, ActivationStatus.Activated, true)]
    [InlineData("Activating", ReleaseTargetStatus.Acknowledged, ActivationStatus.Activating, false)]
    public async Task AcknowledgePullUpdatesTargetAndReleaseStatus(string runtimeStatus, ReleaseTargetStatus expectedTarget, ActivationStatus expectedActivation, bool published)
    {
        var state = CreateState(DistributionMode.RuntimeFetchesFromDesign);
        var service = CreateService(state);

        var result = await service.AcknowledgePull(state.Node.Id.ToString(), state.Target.Id.ToString(), "runtime-artifact-1", runtimeStatus);

        Assert.True(result.Succeeded);
        Assert.Equal(expectedTarget, state.Target.Status);
        Assert.Equal(expectedActivation, state.Target.ActivationStatus);
        Assert.Equal(published, state.Artifact.IsPublished);
        if (published)
        {
            Assert.Equal(ReleaseStatus.Completed, state.ReleaseStatus);
        }
    }

    [Fact]
    public async Task AcknowledgePullRejectsTargetsFromAnotherRuntimeNode()
    {
        var state = CreateState(DistributionMode.RuntimeFetchesFromDesign);
        var otherNode = RuntimeNode(DistributionMode.RuntimeFetchesFromDesign);
        var service = CreateService(state);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcknowledgePull(otherNode.Id.ToString(), state.Target.Id.ToString(), "runtime-artifact-1", "Ready"));

        Assert.Equal("Release target does not belong to the runtime node.", exception.Message);
    }

    [Fact]
    public void DeploymentResponseHelpersHandleEmptyInvalidAndPlainTextRuntimeBodies()
    {
        Assert.Null(InvokePrivateStatic<RuntimeArtifactDeploymentResult>("TryReadDeploymentResponse", " "));
        Assert.Null(InvokePrivateStatic<RuntimeArtifactDeploymentResult>("TryReadDeploymentResponse", "not-json"));
        Assert.Null(InvokePrivateStatic<RuntimeArtifactDeploymentResult>("TryReadDeploymentResponse", """{"accepted":"""));

        Assert.Null(InvokePrivateStatic<string>("ExtractRuntimeError", " "));
        Assert.Equal("runtime failed", InvokePrivateStatic<string>("ExtractRuntimeError", "\r\n runtime failed \n details"));
        Assert.Equal(new string('x', 500), InvokePrivateStatic<string>("ExtractRuntimeError", new string('x', 510)));
    }

    private static T? InvokePrivateStatic<T>(string methodName, params object[] arguments)
    {
        var method = typeof(ArtifactDeliveryApplicationService).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (T?)method.Invoke(null, arguments);
    }

    private static ArtifactDeliveryApplicationService CreateService(
        DeliveryState state,
        RecordingHttpMessageHandler? handler = null)
    {
        handler ??= new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(new RuntimeArtifactDeploymentResult
        {
            Accepted = true,
            RuntimeArtifactId = "runtime-artifact-1",
            Status = "Accepted"
        })));

        return new ArtifactDeliveryApplicationService(
            new HttpClient(handler),
            state.ReleaseTargets,
            state.Releases,
            state.Artifacts,
            state.RuntimeNodes,
            new RecordingRuntimeAccessTokenProvider());
    }

    private static DeliveryState CreateState(DistributionMode mode)
    {
        var node = RuntimeNode(mode);
        var artifact = new Artifact
        {
            Id = Id.New(),
            OrchestrationDefinitionId = "definition-1",
            OrchestrationVersionId = "version-1",
            OrchestrationDisplayName = "Sale Created",
            VersionLabel = "1.0.0",
            VersionNumber = "1.0.0",
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0.0",
            Payload = """{"Key":"sales.sale.created"}""",
            Metadata = "{}",
            SourceEvent = "OrchestrationVersionDeployed",
            SourceVersion = "1.0.0",
            Checksum = "checksum",
            CreatedAtUtc = DateTime.UtcNow
        };
        var target = new ReleaseTarget
        {
            Id = Id.New(),
            RuntimeNodeId = node.Id,
            ArtifactId = artifact.Id,
            ReleaseId = Id.New(),
            RolloutGroup = "default",
            Status = ReleaseTargetStatus.Pending,
            ActivationStatus = ActivationStatus.NotActivated,
            AssignedAtUtc = DateTime.UtcNow,
            FailureReason = string.Empty,
            RuntimeVersionApplied = string.Empty,
            CorrelationId = "correlation"
        };
        var state = new DeliveryState(node, artifact, target);
        state.Nodes[node.Id] = node;
        state.ArtifactRows[artifact.Id] = artifact;
        state.Targets[target.Id] = target;
        return state;
    }

    private static RuntimeNode RuntimeNode(DistributionMode mode)
        => new()
        {
            Id = Id.New(),
            Name = "Local Runtime",
            Code = "local-runtime",
            DistributionMode = mode,
            EndpointBaseUri = "https://runtime.local",
            EndpointApiPath = string.Empty,
            Status = RuntimeNodeStatus.Enabled,
            IsEnabled = true,
            Description = "Runtime node",
            AccessTokenTtlSeconds = 86_400,
            TokenRefreshSkewSeconds = 300,
            TokenValidationCacheTtlSeconds = 300,
            InboundCredentialStatus = ConnectionCredentialStatus.Active,
            OutboundCredentialStatus = ConnectionCredentialStatus.Active,
            RegisteredAtUtc = DateTime.UtcNow
        };

    private sealed class DeliveryState
    {
        public DeliveryState(RuntimeNode node, Artifact artifact, ReleaseTarget target)
        {
            Node = node;
            Artifact = artifact;
            Target = target;
            RuntimeNodes = new RuntimeNodeRepository(Nodes);
            Artifacts = new ArtifactRepository(ArtifactRows);
            Releases = new ReleaseRepository(this);
            ReleaseTargets = new ReleaseTargetRepository(this);
        }

        public RuntimeNode Node { get; }

        public Artifact Artifact { get; }

        public ReleaseTarget Target { get; }

        public Dictionary<Id, RuntimeNode> Nodes { get; } = new();

        public Dictionary<Id, Artifact> ArtifactRows { get; } = new();

        public Dictionary<Id, ReleaseTarget> Targets { get; } = new();

        public List<ReleaseAttempt> Attempts { get; } = new();

        public ReleaseStatus? ReleaseStatus { get; set; }

        public IRuntimeNodeRepository RuntimeNodes { get; }

        public IArtifactRepository Artifacts { get; }

        public IReleaseRepository Releases { get; }

        public IReleaseTargetRepository ReleaseTargets { get; }
    }

    private sealed class RuntimeNodeRepository : IRuntimeNodeRepository
    {
        private readonly Dictionary<Id, RuntimeNode> _nodes;

        public RuntimeNodeRepository(Dictionary<Id, RuntimeNode> nodes) => _nodes = nodes;

        public Task Create(RuntimeNode runtimeNode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task Update(RuntimeNode runtimeNode, CancellationToken cancellationToken = default)
        {
            _nodes[runtimeNode.Id] = runtimeNode;
            return Task.CompletedTask;
        }

        public Task SetStatus(Id runtimeNodeId, RuntimeNodeStatus status, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SoftDelete(Id runtimeNodeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuntimeNode> GetById(Id runtimeNodeId, CancellationToken cancellationToken = default) => Task.FromResult(_nodes[runtimeNodeId]);

        public Task<RuntimeNode> GetByCode(string code, CancellationToken cancellationToken = default) => Task.FromResult(_nodes.Values.First(x => x.Code == code));

        public Task<RuntimeNode> GetByInboundClientId(string clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PagedResult<RuntimeNode>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ArtifactRepository : IArtifactRepository
    {
        private readonly Dictionary<Id, Artifact> _artifacts;

        public ArtifactRepository(Dictionary<Id, Artifact> artifacts) => _artifacts = artifacts;

        public Task Create(Artifact artifact, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SetPublished(Id artifactId, bool isPublished, CancellationToken cancellationToken = default)
        {
            _artifacts[artifactId].IsPublished = isPublished;
            _artifacts[artifactId].PublishedAtUtc = isPublished ? DateTime.UtcNow : null;
            return Task.CompletedTask;
        }

        public Task<Artifact> GetById(Id artifactId, CancellationToken cancellationToken = default) => Task.FromResult(_artifacts[artifactId]);

        public Task<Artifact> GetLatestForOrchestrationVersion(Id orchestrationVersionId, string artifactType, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PagedResult<Artifact>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ReleaseRepository : IReleaseRepository
    {
        private readonly DeliveryState _state;

        public ReleaseRepository(DeliveryState state) => _state = state;

        public Task Create(Release promotion, IReadOnlyCollection<ReleasePlanTarget> targets, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ApplyTargetStatus(Id releaseId, Id runtimeNodeId, ReleaseStatus status, CancellationToken cancellationToken = default)
        {
            _state.ReleaseStatus = status;
            return Task.CompletedTask;
        }

        public Task<Release> GetById(Id promotionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<ReleasePlanTarget>> GetTargets(Id promotionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PagedResult<Release>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ReleaseTargetRepository : IReleaseTargetRepository
    {
        private readonly DeliveryState _state;

        public ReleaseTargetRepository(DeliveryState state) => _state = state;

        public Task Create(ReleaseTarget assignment, CancellationToken cancellationToken = default)
        {
            _state.Targets[assignment.Id] = assignment;
            return Task.CompletedTask;
        }

        public Task Update(ReleaseTarget assignment, CancellationToken cancellationToken = default)
        {
            _state.Targets[assignment.Id] = assignment;
            return Task.CompletedTask;
        }

        public Task AddAttempt(ReleaseAttempt attempt, CancellationToken cancellationToken = default)
        {
            _state.Attempts.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<ReleaseTarget> GetById(Id assignmentId, CancellationToken cancellationToken = default) => Task.FromResult(_state.Targets[assignmentId]);

        public Task<ReleaseTarget> GetByArtifactAndRuntimeNode(Id artifactId, Id runtimeNodeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<ReleaseAttempt>> GetAttempts(Id assignmentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ReleaseAttempt>>(_state.Attempts.Where(x => x.ReleaseTargetId == assignmentId).ToArray());

        public Task<IReadOnlyCollection<ReleaseTarget>> GetPendingForRuntimeNode(Id runtimeNodeId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ReleaseTarget>>(
                _state.Targets.Values.Where(x => x.RuntimeNodeId == runtimeNodeId && x.Status is ReleaseTargetStatus.Pending or ReleaseTargetStatus.PushScheduled or ReleaseTargetStatus.AvailableForPull).ToArray());

        public Task<PagedResult<ReleaseTarget>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingRuntimeAccessTokenProvider : IRuntimeAccessTokenProvider
    {
        public Task AttachTokenAsync(HttpRequestMessage request, RuntimeNode node, IReadOnlyCollection<ArtifactDeliveryScope> scopes, CancellationToken cancellationToken = default)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
            return Task.CompletedTask;
        }
    }
}
