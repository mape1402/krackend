using System.Net;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class RuntimeDesignNodeConnectionServiceTests
{
    [Fact]
    public async Task GenerateCredentialPackagePersistsInboundCredentialAndReturnsRuntimePackage()
    {
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = RuntimeNode(DistributionConnectionMode.DesignPublishesToRuntime);
        await repository.UpsertAsync(node);
        var serializer = new ConnectionCredentialPackageSerializer();
        var service = CreateService(repository, packageSerializer: serializer);

        var result = await service.GenerateCredentialPackageAsync(node.Id.ToString(), "https://runtime.local/");

        var persisted = await repository.GetByIdAsync(node.Id);
        var package = serializer.Parse(result.Json);
        Assert.False(string.IsNullOrWhiteSpace(result.Base64));
        Assert.Equal("runtime", package.Issuer);
        Assert.Equal("design", package.Target);
        Assert.Equal("https://runtime.local", package.BaseUrl);
        Assert.Equal(node.Id.ToString(), package.IssuerNodeId);
        Assert.Equal("runtime-client", package.ClientId);
        Assert.Equal("runtime-secret", package.ClientSecret);
        Assert.Equal("runtime-key", package.KeyId);
        Assert.Equal("artifact:push connection:validate", package.Scopes);
        Assert.Equal("runtime-client", persisted.InboundClientId);
        Assert.Equal("hashed-runtime-secret", persisted.InboundSecretHash);
        Assert.Equal(ConnectionCredentialStatus.Active, persisted.InboundCredentialStatus);
        Assert.NotNull(persisted.InboundCredentialCreatedAtUtc);
        Assert.NotNull(persisted.InboundCredentialRotatedAtUtc);
    }

    [Fact]
    public async Task GenerateCredentialPackageRejectsPullOnlyRuntimeNode()
    {
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = RuntimeNode(DistributionConnectionMode.RuntimeFetchesFromDesign);
        await repository.UpsertAsync(node);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateCredentialPackageAsync(node.Id.ToString(), "https://runtime.local"));

        Assert.Equal("Runtime credentials are only required when Design can call Runtime.", exception.Message);
    }

    [Fact]
    public async Task ImportCredentialPackagePersistsOutboundDesignCredentials()
    {
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = RuntimeNode(DistributionConnectionMode.RuntimeFetchesFromDesign);
        await repository.UpsertAsync(node);
        var serializer = new ConnectionCredentialPackageSerializer();
        var package = serializer.CreateEnvelope(new ConnectionCredentialPackage
        {
            Issuer = "design",
            Target = "runtime",
            Mode = "RuntimeFetchesFromDesign",
            BaseUrl = "https://design.local/",
            TargetNodeId = "runtime-node-at-design",
            ClientId = "design-client",
            ClientSecret = "design-secret",
            KeyId = "design-key",
            Scopes = "release:read artifact:read artifact:ack connection:validate"
        });
        var service = CreateService(repository, packageSerializer: serializer);

        await service.ImportCredentialPackageAsync(new ImportRuntimeDesignNodeCredentialPackageInput
        {
            DesignNodeId = node.Id.ToString(),
            Package = package.Base64
        });

        var persisted = await repository.GetByIdAsync(node.Id);
        Assert.Equal("https://design.local", persisted.EndpointBaseUri);
        Assert.Equal("runtime-node-at-design", persisted.RemoteRuntimeNodeId);
        Assert.Equal("design-client", persisted.OutboundClientId);
        Assert.Equal("design-key", persisted.OutboundKeyId);
        Assert.Equal("protected-design-secret", persisted.ProtectedOutboundSecret);
        Assert.Equal("release:read artifact:read artifact:ack connection:validate", persisted.OutboundRequestedScopes);
        Assert.Equal(ConnectionCredentialStatus.Active, persisted.OutboundCredentialStatus);
        Assert.NotNull(persisted.OutboundCredentialImportedAtUtc);
    }

    [Fact]
    public async Task ImportCredentialPackageRejectsPackageIssuedForWrongDirection()
    {
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = RuntimeNode(DistributionConnectionMode.RuntimeFetchesFromDesign);
        await repository.UpsertAsync(node);
        var serializer = new ConnectionCredentialPackageSerializer();
        var package = serializer.CreateEnvelope(new ConnectionCredentialPackage
        {
            Issuer = "runtime",
            Target = "design",
            ClientId = "runtime-client",
            ClientSecret = "runtime-secret",
            KeyId = "runtime-key"
        });
        var service = CreateService(repository, packageSerializer: serializer);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportCredentialPackageAsync(new ImportRuntimeDesignNodeCredentialPackageInput
            {
                DesignNodeId = node.Id.ToString(),
                Package = package.Json
            }));

        Assert.Equal("Credential package must be issued by Design for Runtime.", exception.Message);
    }

    [Fact]
    public async Task ImportCredentialPackageRejectsPushOnlyRuntimeNode()
    {
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = RuntimeNode(DistributionConnectionMode.DesignPublishesToRuntime);
        await repository.UpsertAsync(node);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportCredentialPackageAsync(new ImportRuntimeDesignNodeCredentialPackageInput
            {
                DesignNodeId = node.Id.ToString(),
                Package = "{}"
            }));

        Assert.Equal("Design credentials are only required when Runtime can call Design.", exception.Message);
    }

    [Fact]
    public async Task ValidateConnectionCallsDesignValidationEndpointWithBearerToken()
    {
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = RuntimeNode(DistributionConnectionMode.RuntimeFetchesFromDesign);
        node.Status = RuntimeDesignNodeStatus.Enabled;
        node.IsEnabled = true;
        node.EndpointBaseUri = "https://design.local/";
        node.RemoteRuntimeNodeId = "runtime-node-at-design";
        node.OutboundCredentialStatus = ConnectionCredentialStatus.Active;
        await repository.UpsertAsync(node);
        var tokenProvider = new RecordingControlPlaneAccessTokenProvider();
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("""{"ok":true}"""));
        var service = CreateService(repository, httpClient: new HttpClient(handler), accessTokenProvider: tokenProvider);

        var result = await service.ValidateConnectionAsync(node.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Equal("Connection validated.", result.Message);
        Assert.Equal(new Uri("https://design.local/distribution/runtime-nodes/runtime-node-at-design/connect/validate"), handler.RequestUri);
        Assert.Equal("Bearer", handler.LastAuthorizationScheme);
        Assert.Equal([ArtifactDeliveryScope.ConnectionValidate], tokenProvider.Scopes);
        Assert.Equal(node.Key, tokenProvider.Source!.Key);
    }

    [Fact]
    public async Task ValidateConnectionReturnsFailureWithoutThrowingWhenNodeIsNotReady()
    {
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = RuntimeNode(DistributionConnectionMode.RuntimeFetchesFromDesign);
        node.Status = RuntimeDesignNodeStatus.Pending;
        node.IsEnabled = false;
        await repository.UpsertAsync(node);
        var service = CreateService(repository);

        var result = await service.ValidateConnectionAsync(node.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Equal("Design node must be enabled before checking the connection.", result.Message);
    }

    [Theory]
    [InlineData(DistributionConnectionMode.DesignPublishesToRuntime, ConnectionCredentialStatus.Active, "https://design.local", "runtime-node-at-design", "Connection check only applies when Runtime can call Design.")]
    [InlineData(DistributionConnectionMode.RuntimeFetchesFromDesign, ConnectionCredentialStatus.Missing, "https://design.local", "runtime-node-at-design", "Design credentials are required before checking the connection.")]
    [InlineData(DistributionConnectionMode.RuntimeFetchesFromDesign, ConnectionCredentialStatus.Active, "", "runtime-node-at-design", "Design endpoint is required before checking the connection.")]
    [InlineData(DistributionConnectionMode.RuntimeFetchesFromDesign, ConnectionCredentialStatus.Active, "https://design.local", "", "Remote runtime node id is required before checking the connection.")]
    public async Task ValidateConnectionReturnsFailureForIncompletePullConnection(
        DistributionConnectionMode mode,
        ConnectionCredentialStatus outboundStatus,
        string endpoint,
        string remoteRuntimeNodeId,
        string expectedMessage)
    {
        var repository = new FakeRuntimeDesignNodeRepository();
        var node = RuntimeNode(mode);
        node.Status = RuntimeDesignNodeStatus.Enabled;
        node.IsEnabled = true;
        node.OutboundCredentialStatus = outboundStatus;
        node.EndpointBaseUri = endpoint;
        node.RemoteRuntimeNodeId = remoteRuntimeNodeId;
        await repository.UpsertAsync(node);
        var service = CreateService(repository);

        var result = await service.ValidateConnectionAsync(node.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Equal(expectedMessage, result.Message);
    }

    private static RuntimeDesignNodeConnectionService CreateService(
        IRuntimeDesignNodeRepository repository,
        HttpClient? httpClient = null,
        ConnectionCredentialPackageSerializer? packageSerializer = null,
        IControlPlaneAccessTokenProvider? accessTokenProvider = null)
        => new(
            httpClient ?? new HttpClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}"))),
            repository,
            new FixedConnectionSecretGenerator(),
            new PrefixingConnectionSecretHasher(),
            new DefaultConnectionScopeFormatter(),
            packageSerializer ?? new ConnectionCredentialPackageSerializer(),
            new PrefixingRuntimeDesignNodeSecretProtector(),
            accessTokenProvider ?? new RecordingControlPlaneAccessTokenProvider());

    private static RuntimeDesignNode RuntimeNode(DistributionConnectionMode mode)
        => new()
        {
            Id = Id.New(),
            Key = "local-design",
            Name = "Local Design",
            DistributionMode = mode,
            AccessTokenTtlSeconds = 3600,
            TokenRefreshSkewSeconds = 30,
            TokenValidationCacheTtlSeconds = 60
        };

    private sealed class FixedConnectionSecretGenerator : IConnectionSecretGenerator
    {
        public ConnectionCredentialMaterial GenerateCredential(string prefix)
            => new()
            {
                ClientId = "runtime-client",
                ClientSecret = "runtime-secret",
                KeyId = "runtime-key"
            };

        public string GenerateToken() => "runtime-token";
    }

    private sealed class PrefixingConnectionSecretHasher : IConnectionSecretHasher
    {
        public string HashSecret(string secret) => $"hashed-{secret}";

        public bool VerifySecret(string secret, string storedHash) => HashSecret(secret) == storedHash;
    }

    private sealed class PrefixingRuntimeDesignNodeSecretProtector : IRuntimeDesignNodeSecretProtector
    {
        public string Protect(string secret) => $"protected-{secret}";

        public string Unprotect(string protectedSecret)
            => protectedSecret.StartsWith("protected-", StringComparison.Ordinal)
                ? protectedSecret["protected-".Length..]
                : protectedSecret;
    }

    private sealed class RecordingControlPlaneAccessTokenProvider : IControlPlaneAccessTokenProvider
    {
        public ControlPlaneDistributionSource? Source { get; private set; }

        public IReadOnlyCollection<ArtifactDeliveryScope> Scopes { get; private set; } = [];

        public Task AttachTokenAsync(
            HttpRequestMessage request,
            ControlPlaneDistributionSource source,
            IReadOnlyCollection<ArtifactDeliveryScope> scopes,
            CancellationToken cancellationToken = default)
        {
            Source = source;
            Scopes = scopes;
            request.Headers.Authorization = new("Bearer", "runtime-token");
            return Task.CompletedTask;
        }
    }
}
