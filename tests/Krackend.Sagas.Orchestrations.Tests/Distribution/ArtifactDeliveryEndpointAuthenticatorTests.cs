using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class ArtifactDeliveryEndpointAuthenticatorTests
{
    [Theory]
    [InlineData("GET", "/orchestrator/distribution/runtime-nodes/runtime-1/releases/pending", ArtifactDeliveryScope.ReleaseRead)]
    [InlineData("GET", "/orchestrator/distribution/runtime-nodes/runtime-1/artifacts/artifact-1", ArtifactDeliveryScope.ArtifactRead)]
    [InlineData("POST", "/orchestrator/distribution/runtime-nodes/runtime-1/releases/release-1/ack", ArtifactDeliveryScope.ArtifactAcknowledge)]
    public async Task ControlPlaneAuthenticatorReadsBearerTokenAndResolvesRequiredScope(
        string method,
        string path,
        ArtifactDeliveryScope expectedScope)
    {
        var validator = Substitute.For<IControlPlaneConnectionTokenValidator>();
        string? token = null;
        string? runtimeNodeId = null;
        IReadOnlyCollection<ArtifactDeliveryScope>? scopes = null;
        validator.ValidateAsync(
                Arg.Do<string>(value => token = value),
                Arg.Do<string>(value => runtimeNodeId = value),
                Arg.Do<IReadOnlyCollection<ArtifactDeliveryScope>>(value => scopes = value),
                Arg.Any<CancellationToken>())
            .Returns(ConnectionTokenValidationResult.Success(new ConnectionTokenPrincipal
            {
                NodeId = "runtime-1",
                NodeKey = "runtime-node"
            }));
        var authenticator = new ArtifactDeliveryEndpointAuthenticator(validator);
        var request = Request(method, path, "Bearer token-123");

        var result = await authenticator.AuthenticateRuntimeNodeAsync(request, "runtime-1", "{}");

        Assert.True(result.Succeeded);
        Assert.Equal("token-123", token);
        Assert.Equal("runtime-1", runtimeNodeId);
        Assert.Equal([expectedScope], scopes);
    }

    [Fact]
    public async Task ControlPlaneAuthenticatorReturnsFailureWhenTokenValidationFails()
    {
        var validator = Substitute.For<IControlPlaneConnectionTokenValidator>();
        validator.ValidateAsync(
                string.Empty,
                "runtime-1",
                Arg.Is<IReadOnlyCollection<ArtifactDeliveryScope>>(scopes =>
                    scopes.Count == 1 && scopes.Contains(ArtifactDeliveryScope.ArtifactRead)),
                Arg.Any<CancellationToken>())
            .Returns(ConnectionTokenValidationResult.Failure("missing token"));
        var authenticator = new ArtifactDeliveryEndpointAuthenticator(validator);

        var result = await authenticator.AuthenticateRuntimeNodeAsync(
            Request("GET", "/orchestrator/distribution/runtime-nodes/runtime-1/artifacts/artifact-1"),
            "runtime-1",
            "{}");

        Assert.False(result.Succeeded);
        Assert.Equal("missing token", result.Message);
        Assert.Throws<ArgumentNullException>(() => new ArtifactDeliveryEndpointAuthenticator(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => authenticator.AuthenticateRuntimeNodeAsync(null!, "runtime-1", "{}"));
    }

    [Fact]
    public async Task RuntimeAuthenticatorRequiresArtifactPushScopeAndReturnsNodeKey()
    {
        var validator = Substitute.For<IRuntimeConnectionTokenValidator>();
        string? token = null;
        IReadOnlyCollection<ArtifactDeliveryScope>? scopes = null;
        validator.ValidateAsync(
                Arg.Do<string>(value => token = value),
                Arg.Do<IReadOnlyCollection<ArtifactDeliveryScope>>(value => scopes = value),
                Arg.Any<CancellationToken>())
            .Returns(ConnectionTokenValidationResult.Success(new ConnectionTokenPrincipal
            {
                NodeId = "design-1",
                NodeKey = "design-node"
            }));
        var authenticator = new RuntimeArtifactDeliveryEndpointAuthenticator(validator);

        var result = await authenticator.AuthenticateAsync(
            Request("POST", "/orchestrator/runtime/artifacts", "Bearer runtime-token"),
            "{}");

        Assert.True(result.Succeeded);
        Assert.Equal("design-node", result.SourceKey);
        Assert.Equal("runtime-token", token);
        Assert.Equal([ArtifactDeliveryScope.ArtifactPush], scopes);
    }

    [Fact]
    public async Task RuntimeAuthenticatorReturnsFailureWhenTokenValidationFails()
    {
        var validator = Substitute.For<IRuntimeConnectionTokenValidator>();
        validator.ValidateAsync(
                string.Empty,
                Arg.Is<IReadOnlyCollection<ArtifactDeliveryScope>>(scopes =>
                    scopes.Count == 1 && scopes.Contains(ArtifactDeliveryScope.ArtifactPush)),
                Arg.Any<CancellationToken>())
            .Returns(ConnectionTokenValidationResult.Failure("forbidden"));
        var authenticator = new RuntimeArtifactDeliveryEndpointAuthenticator(validator);

        var result = await authenticator.AuthenticateAsync(
            Request("POST", "/orchestrator/runtime/artifacts"),
            "{}");

        Assert.False(result.Succeeded);
        Assert.Equal("forbidden", result.Message);
        Assert.Throws<ArgumentNullException>(() => new RuntimeArtifactDeliveryEndpointAuthenticator(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => authenticator.AuthenticateAsync(null!, "{}"));
    }

    private static HttpRequest Request(string method, string path, string authorization = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            context.Request.Headers.Authorization = authorization;
        }

        return context.Request;
    }
}
