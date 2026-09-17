namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using NSubstitute;

public sealed class ControlPlaneArtifactPullServiceTests
{
    [Fact]
    public void GetSourcesReturnsConfiguredDistributionSources()
    {
        var source = Source();
        var provider = new SingleSourceProvider(source);
        var service = CreateService(provider: provider);

        var sources = service.GetSources();

        Assert.Same(source, sources.Single());
    }

    [Fact]
    public async Task GetPendingAsyncUsesSourceTokenAndDeserializesPackages()
    {
        var source = Source();
        var accessTokens = new RecordingAccessTokenProvider();
        var package = Package("target-1");
        var handler = new RecordingHandler(_ => JsonArray(package));
        var service = CreateService(handler, new SingleSourceProvider(source), accessTokens);

        var pending = await service.GetPendingAsync(source.Key);

        Assert.Single(pending);
        Assert.Equal("target-1", pending.Single().ReleaseTargetId);
        Assert.Equal("/orchestrator/distribution/runtime-nodes/runtime-node-1/artifacts/pending", handler.Requests.Single().RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.Requests.Single().Method);
        Assert.Equal("Bearer", handler.Requests.Single().Headers.Authorization!.Scheme);
        Assert.Contains(ArtifactDeliveryScope.ReleaseRead, accessTokens.RequestedScopes.Single());
    }

    [Fact]
    public async Task ApplyAsyncDownloadsPackageDeploysAndAcknowledgesReleaseTarget()
    {
        var source = Source();
        var accessTokens = new RecordingAccessTokenProvider();
        var package = Package("release-target-1");
        var deployment = new RuntimeArtifactDeploymentResult
        {
            Accepted = true,
            RuntimeArtifactId = "runtime-artifact-1",
            Status = "Ready",
            Message = "Installed"
        };
        var deploymentService = Substitute.For<IRuntimeArtifactDeploymentService>();
        deploymentService
            .DeployAsync(Arg.Any<RuntimeArtifactDeliveryPackage>(), source.Key, Arg.Any<CancellationToken>())
            .Returns(deployment);
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Json(package);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = CreateService(handler, new SingleSourceProvider(source), accessTokens, deploymentService);

        var result = await service.ApplyAsync(source.Key, "release-target-1");

        Assert.Same(deployment, result);
        await deploymentService.Received(1).DeployAsync(
            Arg.Is<RuntimeArtifactDeliveryPackage>(candidate => candidate.ReleaseTargetId == "release-target-1"),
            source.Key,
            Arg.Any<CancellationToken>());
        Assert.Equal([HttpMethod.Get, HttpMethod.Post], handler.Requests.Select(request => request.Method).ToArray());
        Assert.Equal(
            "/orchestrator/distribution/runtime-nodes/runtime-node-1/artifacts/release-target-1/ack",
            handler.Requests.Last().RequestUri!.AbsolutePath);
        Assert.Contains(accessTokens.RequestedScopes, scopes => scopes.Contains(ArtifactDeliveryScope.ArtifactRead));
        Assert.Contains(accessTokens.RequestedScopes, scopes => scopes.Contains(ArtifactDeliveryScope.ArtifactAcknowledge));
    }

    private static ControlPlaneArtifactPullService CreateService(
        RecordingHandler? handler = null,
        IControlPlaneDistributionSourceProvider? provider = null,
        IControlPlaneAccessTokenProvider? accessTokenProvider = null,
        IRuntimeArtifactDeploymentService? deploymentService = null)
        => new(
            new HttpClient(handler ?? new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            provider ?? new SingleSourceProvider(Source()),
            accessTokenProvider ?? new RecordingAccessTokenProvider(),
            deploymentService ?? Substitute.For<IRuntimeArtifactDeploymentService>());

    private static ControlPlaneDistributionSource Source()
        => new()
        {
            Key = "design",
            Name = "Design",
            EndpointBaseUri = "https://design.local/orchestrator",
            RemoteRuntimeNodeId = "runtime-node-1",
            ClientId = "runtime-client",
            ProtectedSecret = "secret",
            KeyId = "key-id",
            RequestedScopes = "release:read artifact:read artifact:ack",
            IsEnabled = true
        };

    private static RuntimeArtifactDeliveryPackage Package(string releaseTargetId)
        => new()
        {
            ReleaseTargetId = releaseTargetId,
            ArtifactId = "artifact-1",
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0",
            OrchestrationDefinitionId = "definition-1",
            OrchestrationVersionId = "version-1",
            OrchestrationDefinitionKey = "sales.sale.created",
            Version = "1.0.0",
            Checksum = "checksum",
            PayloadJson = "{}",
            CorrelationId = "correlation-1",
            PromotedBy = "operator",
            PromotedOnUtc = DateTime.UtcNow
        };

    private static HttpResponseMessage JsonArray(params RuntimeArtifactDeliveryPackage[] packages)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(packages))
        };

    private static HttpResponseMessage Json(RuntimeArtifactDeliveryPackage package)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(package))
        };

    private sealed class SingleSourceProvider : IControlPlaneDistributionSourceProvider
    {
        private readonly ControlPlaneDistributionSource _source;

        public SingleSourceProvider(ControlPlaneDistributionSource source)
        {
            _source = source;
        }

        public IReadOnlyCollection<ControlPlaneDistributionSource> GetAll()
            => [_source];

        public ControlPlaneDistributionSource GetByKey(string sourceKey)
            => _source;

        public ControlPlaneDistributionSource GetByClientId(string clientId)
            => _source;
    }

    private sealed class RecordingAccessTokenProvider : IControlPlaneAccessTokenProvider
    {
        public List<IReadOnlyCollection<ArtifactDeliveryScope>> RequestedScopes { get; } = [];

        public Task AttachTokenAsync(
            HttpRequestMessage request,
            ControlPlaneDistributionSource source,
            IReadOnlyCollection<ArtifactDeliveryScope> scopes,
            CancellationToken cancellationToken = default)
        {
            RequestedScopes.Add(scopes);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"token-{source.Key}");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequest(request));
            return Task.FromResult(_responseFactory(request));
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            clone.Headers.Authorization = request.Headers.Authorization;
            return clone;
        }
    }
}
