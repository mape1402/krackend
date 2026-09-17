using System.Net;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class AccessTokenProviderTests
{
    [Fact]
    public async Task DesignProviderRequestsRuntimeTokenOnceAndReusesCachedToken()
    {
        var calls = 0;
        ConnectionTokenRequest? capturedRequest = null;
        var handler = new RecordingHttpMessageHandler(request =>
        {
            calls++;
            capturedRequest = ReadTokenRequest(request);
            return RecordingHttpMessageHandler.Json(TokenJson("runtime-token", DateTime.UtcNow.AddMinutes(15), "runtime-key"));
        });
        var provider = new RuntimeAccessTokenProvider(
            new HttpClient(handler),
            new EchoControlPlaneRuntimeNodeSecretProtector(),
            new DefaultConnectionScopeFormatter(),
            new ConnectionTokenCacheKeyBuilder(),
            CreateCache());
        var node = RuntimeNode();
        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "https://runtime.local/protected");
        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "https://runtime.local/protected-again");

        await provider.AttachTokenAsync(firstRequest, node, [ArtifactDeliveryScope.ArtifactPush]);
        await provider.AttachTokenAsync(secondRequest, node, [ArtifactDeliveryScope.ArtifactPush]);

        Assert.Equal(1, calls);
        Assert.Equal("Bearer", firstRequest.Headers.Authorization!.Scheme);
        Assert.Equal("runtime-token", firstRequest.Headers.Authorization.Parameter);
        Assert.Equal("runtime-token", secondRequest.Headers.Authorization!.Parameter);
        Assert.Equal(new Uri("https://runtime.local/runtime/distribution/connect/token"), handler.RequestUri);
        Assert.Equal("runtime-client", capturedRequest!.ClientId);
        Assert.Equal("runtime-secret", capturedRequest.ClientSecret);
        Assert.Equal("artifact:push", capturedRequest.Scope);
    }

    [Fact]
    public async Task DesignProviderRefreshesCachedTokenWhenItIsInsideSkewWindow()
    {
        var calls = 0;
        var handler = new RecordingHttpMessageHandler(_ =>
        {
            calls++;
            var token = calls == 1 ? "soon-expiring-token" : "fresh-token";
            var expiration = calls == 1 ? DateTime.UtcNow.AddSeconds(20) : DateTime.UtcNow.AddMinutes(15);
            return RecordingHttpMessageHandler.Json(TokenJson(token, expiration, "runtime-key"));
        });
        var provider = new RuntimeAccessTokenProvider(
            new HttpClient(handler),
            new EchoControlPlaneRuntimeNodeSecretProtector(),
            new DefaultConnectionScopeFormatter(),
            new ConnectionTokenCacheKeyBuilder(),
            CreateCache());
        var node = RuntimeNode();
        node.TokenRefreshSkewSeconds = 60;
        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "https://runtime.local/protected");
        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "https://runtime.local/protected-again");

        await provider.AttachTokenAsync(firstRequest, node, [ArtifactDeliveryScope.ArtifactPush]);
        await provider.AttachTokenAsync(secondRequest, node, [ArtifactDeliveryScope.ArtifactPush]);

        Assert.Equal(2, calls);
        Assert.Equal("soon-expiring-token", firstRequest.Headers.Authorization!.Parameter);
        Assert.Equal("fresh-token", secondRequest.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task DesignProviderRejectsRuntimeNodeWithoutOutboundCredentials()
    {
        var provider = new RuntimeAccessTokenProvider(
            new HttpClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}"))),
            new EchoControlPlaneRuntimeNodeSecretProtector(),
            new DefaultConnectionScopeFormatter(),
            new ConnectionTokenCacheKeyBuilder(),
            CreateCache());
        var node = RuntimeNode();
        node.ProtectedOutboundSecret = "";
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://runtime.local/protected");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.AttachTokenAsync(request, node, [ArtifactDeliveryScope.ArtifactPush]));

        Assert.Contains("does not have outbound credentials", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeProviderRequestsDesignTokenOnceAndReusesCachedToken()
    {
        var calls = 0;
        ConnectionTokenRequest? capturedRequest = null;
        var handler = new RecordingHttpMessageHandler(request =>
        {
            calls++;
            capturedRequest = ReadTokenRequest(request);
            return RecordingHttpMessageHandler.Json(TokenJson("design-token", DateTime.UtcNow.AddMinutes(15), "design-key"));
        });
        var provider = new ControlPlaneAccessTokenProvider(
            new HttpClient(handler),
            new EchoRuntimeDesignNodeSecretProtector(),
            new DefaultConnectionScopeFormatter(),
            new ConnectionTokenCacheKeyBuilder(),
            CreateCache());
        var source = ControlPlaneSource();
        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "https://design.local/protected");
        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "https://design.local/protected-again");

        await provider.AttachTokenAsync(firstRequest, source, [ArtifactDeliveryScope.ReleaseRead]);
        await provider.AttachTokenAsync(secondRequest, source, [ArtifactDeliveryScope.ReleaseRead]);

        Assert.Equal(1, calls);
        Assert.Equal("Bearer", firstRequest.Headers.Authorization!.Scheme);
        Assert.Equal("design-token", firstRequest.Headers.Authorization.Parameter);
        Assert.Equal("design-token", secondRequest.Headers.Authorization!.Parameter);
        Assert.Equal(new Uri("https://design.local/distribution/connect/token"), handler.RequestUri);
        Assert.Equal("design-client", capturedRequest!.ClientId);
        Assert.Equal("design-secret", capturedRequest.ClientSecret);
        Assert.Equal("release:read", capturedRequest.Scope);
    }

    [Fact]
    public async Task RuntimeProviderRejectsExpiredDesignTokenResponse()
    {
        var provider = new ControlPlaneAccessTokenProvider(
            new HttpClient(new RecordingHttpMessageHandler(_ =>
                RecordingHttpMessageHandler.Json(TokenJson("expired-token", DateTime.UtcNow.AddSeconds(-1), "design-key")))),
            new EchoRuntimeDesignNodeSecretProtector(),
            new DefaultConnectionScopeFormatter(),
            new ConnectionTokenCacheKeyBuilder(),
            CreateCache());
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://design.local/protected");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.AttachTokenAsync(request, ControlPlaneSource(), [ArtifactDeliveryScope.ReleaseRead]));

        Assert.Equal("Design returned an expired token.", exception.Message);
    }

    [Fact]
    public async Task RuntimeProviderSurfacesDesignTokenHttpFailure()
    {
        var provider = new ControlPlaneAccessTokenProvider(
            new HttpClient(new RecordingHttpMessageHandler(_ =>
                RecordingHttpMessageHandler.Json("""{"error":"invalid_client"}""", HttpStatusCode.Unauthorized))),
            new EchoRuntimeDesignNodeSecretProtector(),
            new DefaultConnectionScopeFormatter(),
            new ConnectionTokenCacheKeyBuilder(),
            CreateCache());
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://design.local/protected");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.AttachTokenAsync(request, ControlPlaneSource(), [ArtifactDeliveryScope.ReleaseRead]));
    }

    private static MemoryDistributedCache CreateCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));

    private static RuntimeNode RuntimeNode()
        => new()
        {
            Id = Id.New(),
            Code = "local-runtime",
            Name = "Local Runtime",
            EndpointBaseUri = "https://runtime.local/",
            OutboundClientId = "runtime-client",
            OutboundKeyId = "runtime-key",
            ProtectedOutboundSecret = "runtime-secret",
            TokenRefreshSkewSeconds = 30
        };

    private static ControlPlaneDistributionSource ControlPlaneSource()
        => new()
        {
            Key = "local-design",
            Name = "Local Design",
            EndpointBaseUri = "https://design.local/",
            RemoteRuntimeNodeId = Id.New().ToString(),
            ClientId = "design-client",
            KeyId = "design-key",
            ProtectedSecret = "design-secret",
            TokenRefreshSkewSeconds = 30
        };

    private static ConnectionTokenRequest ReadTokenRequest(HttpRequestMessage request)
    {
        var json = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        return JsonSerializer.Deserialize<ConnectionTokenRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static string TokenJson(string accessToken, DateTime expiresAtUtc, string keyId)
        => JsonSerializer.Serialize(new ConnectionTokenResponse
        {
            AccessToken = accessToken,
            ExpiresAtUtc = expiresAtUtc,
            ExpiresIn = (int)Math.Max(0, (expiresAtUtc - DateTime.UtcNow).TotalSeconds),
            KeyId = keyId,
            Scope = "artifact:push release:read"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private sealed class EchoControlPlaneRuntimeNodeSecretProtector : IControlPlaneRuntimeNodeSecretProtector
    {
        public string Protect(string secret) => secret;

        public string Unprotect(string protectedSecret) => protectedSecret;
    }

    private sealed class EchoRuntimeDesignNodeSecretProtector : IRuntimeDesignNodeSecretProtector
    {
        public string Protect(string secret) => secret;

        public string Unprotect(string protectedSecret) => protectedSecret;
    }
}
