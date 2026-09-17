using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class RuntimeNodeConnectionApplicationServiceTests
{
    [Fact]
    public async Task GenerateCredentialPackagePersistsInboundCredentialAndReturnsDesignPackage()
    {
        var repository = new FakeRuntimeNodeRepository();
        var node = RuntimeNode(DistributionMode.RuntimeFetchesFromDesign);
        await repository.Create(node);
        var serializer = new ConnectionCredentialPackageSerializer();
        var service = CreateService(repository, packageSerializer: serializer);

        var result = await service.GenerateCredentialPackage(node.Id.ToString(), "https://design.local/");

        var persisted = await repository.GetById(node.Id);
        var package = serializer.Parse(result.Json);
        Assert.False(string.IsNullOrWhiteSpace(result.Base64));
        Assert.Equal("design", package.Issuer);
        Assert.Equal("runtime", package.Target);
        Assert.Equal("https://design.local", package.BaseUrl);
        Assert.Equal(node.Id.ToString(), package.TargetNodeId);
        Assert.Equal("design-client", package.ClientId);
        Assert.Equal("design-secret", package.ClientSecret);
        Assert.Equal("design-key", package.KeyId);
        Assert.Equal("release:read artifact:read artifact:ack connection:validate", package.Scopes);
        Assert.Equal("design-client", persisted.InboundClientId);
        Assert.Equal("hashed-design-secret", persisted.InboundSecretHash);
        Assert.Equal(ConnectionCredentialStatus.Active, persisted.InboundCredentialStatus);
        Assert.NotNull(persisted.InboundCredentialCreatedAtUtc);
        Assert.NotNull(persisted.InboundCredentialRotatedAtUtc);
    }

    [Fact]
    public async Task ImportCredentialPackagePersistsOutboundRuntimeCredentials()
    {
        var repository = new FakeRuntimeNodeRepository();
        var node = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        await repository.Create(node);
        var serializer = new ConnectionCredentialPackageSerializer();
        var package = serializer.CreateEnvelope(new ConnectionCredentialPackage
        {
            Issuer = "runtime",
            Target = "design",
            Mode = "DesignPublishesToRuntime",
            BaseUrl = "https://runtime.local/",
            ClientId = "runtime-client",
            ClientSecret = "runtime-secret",
            KeyId = "runtime-key",
            Scopes = "artifact:push connection:validate"
        });
        var service = CreateService(repository, packageSerializer: serializer);

        await service.ImportCredentialPackage(new ImportRuntimeNodeCredentialPackageInput
        {
            RuntimeNodeId = node.Id.ToString(),
            Package = package.Json
        });

        var persisted = await repository.GetById(node.Id);
        Assert.Equal("https://runtime.local", persisted.EndpointBaseUri);
        Assert.Equal("runtime-client", persisted.OutboundClientId);
        Assert.Equal("runtime-key", persisted.OutboundKeyId);
        Assert.Equal("protected-runtime-secret", persisted.ProtectedOutboundSecret);
        Assert.Equal("artifact:push connection:validate", persisted.OutboundRequestedScopes);
        Assert.Equal(ConnectionCredentialStatus.Active, persisted.OutboundCredentialStatus);
        Assert.NotNull(persisted.OutboundCredentialImportedAtUtc);
    }

    [Fact]
    public async Task ImportCredentialPackageRejectsPackageIssuedForWrongDirection()
    {
        var repository = new FakeRuntimeNodeRepository();
        var node = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        await repository.Create(node);
        var serializer = new ConnectionCredentialPackageSerializer();
        var package = serializer.CreateEnvelope(new ConnectionCredentialPackage
        {
            Issuer = "design",
            Target = "runtime",
            ClientId = "design-client",
            ClientSecret = "design-secret",
            KeyId = "design-key"
        });
        var service = CreateService(repository, packageSerializer: serializer);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportCredentialPackage(new ImportRuntimeNodeCredentialPackageInput
            {
                RuntimeNodeId = node.Id.ToString(),
                Package = package.Base64
            }));

        Assert.Equal("Credential package must be issued by Runtime for Design.", exception.Message);
    }

    [Fact]
    public async Task ValidateConnectionCallsRuntimeValidationEndpointWithBearerToken()
    {
        var repository = new FakeRuntimeNodeRepository();
        var node = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        node.Status = RuntimeNodeStatus.Enabled;
        node.IsEnabled = true;
        node.EndpointBaseUri = "https://runtime.local/";
        node.OutboundCredentialStatus = ConnectionCredentialStatus.Active;
        await repository.Create(node);
        var tokenProvider = new RecordingRuntimeAccessTokenProvider();
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("""{"ok":true}"""));
        var service = CreateService(repository, httpClient: new HttpClient(handler), accessTokenProvider: tokenProvider);

        var result = await service.ValidateConnection(node.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Equal("Connection validated.", result.Message);
        Assert.Equal(new Uri("https://runtime.local/runtime/distribution/connect/validate"), handler.RequestUri);
        Assert.Equal("Bearer", handler.LastAuthorizationScheme);
        Assert.Equal([ArtifactDeliveryScope.ConnectionValidate], tokenProvider.Scopes);
        Assert.Equal(node.Id, tokenProvider.Node!.Id);
    }

    [Fact]
    public async Task ValidateConnectionReturnsFailureWhenDesignCannotCallRuntime()
    {
        var repository = new FakeRuntimeNodeRepository();
        var node = RuntimeNode(DistributionMode.RuntimeFetchesFromDesign);
        node.Status = RuntimeNodeStatus.Enabled;
        node.IsEnabled = true;
        node.OutboundCredentialStatus = ConnectionCredentialStatus.Active;
        node.EndpointBaseUri = "https://runtime.local";
        await repository.Create(node);
        var service = CreateService(repository);

        var result = await service.ValidateConnection(node.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Equal("Connection check only applies when Design can call Runtime.", result.Message);
    }

    [Fact]
    public async Task ValidateConnectionReturnsResponseBodyWhenRuntimeRejectsRequest()
    {
        var repository = new FakeRuntimeNodeRepository();
        var node = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        node.Status = RuntimeNodeStatus.Enabled;
        node.IsEnabled = true;
        node.EndpointBaseUri = "https://runtime.local";
        node.OutboundCredentialStatus = ConnectionCredentialStatus.Active;
        await repository.Create(node);
        var service = CreateService(
            repository,
            httpClient: new HttpClient(new RecordingHttpMessageHandler(_ =>
                RecordingHttpMessageHandler.Json("""{"error":"forbidden"}""", System.Net.HttpStatusCode.Forbidden))));

        var result = await service.ValidateConnection(node.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Equal("""{"error":"forbidden"}""", result.Message);
    }

    [Theory]
    [InlineData(true, RuntimeNodeStatus.Enabled, ConnectionCredentialStatus.Active, "https://runtime.local", "Runtime node was deleted.")]
    [InlineData(false, RuntimeNodeStatus.Pending, ConnectionCredentialStatus.Active, "https://runtime.local", "Runtime node must be enabled before checking the connection.")]
    [InlineData(false, RuntimeNodeStatus.Enabled, ConnectionCredentialStatus.Missing, "https://runtime.local", "Runtime credentials are required before checking the connection.")]
    [InlineData(false, RuntimeNodeStatus.Enabled, ConnectionCredentialStatus.Active, "", "Runtime endpoint is required before checking the connection.")]
    public async Task ValidateConnectionReturnsFailureWhenRuntimeNodeCannotBeChecked(
        bool isDeleted,
        RuntimeNodeStatus status,
        ConnectionCredentialStatus outboundCredentialStatus,
        string endpoint,
        string expectedMessage)
    {
        var repository = new FakeRuntimeNodeRepository();
        var node = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        node.IsDeleted = isDeleted;
        node.Status = status;
        node.IsEnabled = status == RuntimeNodeStatus.Enabled;
        node.EndpointBaseUri = endpoint;
        node.OutboundCredentialStatus = outboundCredentialStatus;
        await repository.Create(node);
        var service = CreateService(repository);

        var result = await service.ValidateConnection(node.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Equal(expectedMessage, result.Message);
    }

    [Fact]
    public async Task ValidateConnectionReturnsFailureWhenRuntimeNodeIdIsInvalid()
    {
        var service = CreateService(new FakeRuntimeNodeRepository());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ValidateConnection("not-a-ulid"));

        Assert.Equal("Runtime node id is invalid.", exception.Message);
    }

    private static RuntimeNodeConnectionApplicationService CreateService(
        FakeRuntimeNodeRepository repository,
        HttpClient? httpClient = null,
        ConnectionCredentialPackageSerializer? packageSerializer = null,
        IRuntimeAccessTokenProvider? accessTokenProvider = null)
        => new(
            httpClient ?? new HttpClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}"))),
            repository,
            new FixedConnectionSecretGenerator(),
            new PrefixingConnectionSecretHasher(),
            new DefaultConnectionScopeFormatter(),
            packageSerializer ?? new ConnectionCredentialPackageSerializer(),
            new PrefixingControlPlaneRuntimeNodeSecretProtector(),
            accessTokenProvider ?? new RecordingRuntimeAccessTokenProvider());

    private static RuntimeNode RuntimeNode(DistributionMode mode)
        => new()
        {
            Id = Id.New(),
            Code = "local-runtime",
            Name = "Local Runtime",
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
                ClientId = "design-client",
                ClientSecret = "design-secret",
                KeyId = "design-key"
            };

        public string GenerateToken() => "design-token";
    }

    private sealed class PrefixingConnectionSecretHasher : IConnectionSecretHasher
    {
        public string HashSecret(string secret) => $"hashed-{secret}";

        public bool VerifySecret(string secret, string storedHash) => HashSecret(secret) == storedHash;
    }

    private sealed class PrefixingControlPlaneRuntimeNodeSecretProtector : IControlPlaneRuntimeNodeSecretProtector
    {
        public string Protect(string secret) => $"protected-{secret}";

        public string Unprotect(string protectedSecret)
            => protectedSecret.StartsWith("protected-", StringComparison.Ordinal)
                ? protectedSecret["protected-".Length..]
                : protectedSecret;
    }

    private sealed class RecordingRuntimeAccessTokenProvider : IRuntimeAccessTokenProvider
    {
        public RuntimeNode? Node { get; private set; }

        public IReadOnlyCollection<ArtifactDeliveryScope> Scopes { get; private set; } = [];

        public Task AttachTokenAsync(
            HttpRequestMessage request,
            RuntimeNode node,
            IReadOnlyCollection<ArtifactDeliveryScope> scopes,
            CancellationToken cancellationToken = default)
        {
            Node = node;
            Scopes = scopes;
            request.Headers.Authorization = new("Bearer", "design-token");
            return Task.CompletedTask;
        }
    }
}
