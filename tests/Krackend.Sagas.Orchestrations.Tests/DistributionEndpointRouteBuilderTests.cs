using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests;

public sealed class DistributionEndpointRouteBuilderTests
{
    [Fact]
    public async Task ControlPlaneDistributionEndpointsHandleTokenValidationPushPullAndAck()
    {
        var service = Substitute.For<IArtifactDeliveryApplicationService>();
        var authenticator = Substitute.For<IArtifactDeliveryEndpointAuthenticator>();
        var issuer = Substitute.For<IControlPlaneConnectionTokenIssuer>();
        var validator = Substitute.For<IControlPlaneConnectionTokenValidator>();
        var package = CreatePackage("release-1");
        var delivery = new RuntimeArtifactDeliveryResult
        {
            Succeeded = true,
            ReleaseTargetId = "release-1",
            RuntimeNodeId = "runtime-1",
            ArtifactId = "artifact-1",
            Status = "Delivered"
        };

        issuer.IssueAsync(Arg.Any<ConnectionTokenRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ConnectionTokenResponse
            {
                AccessToken = "token",
                ExpiresIn = 3600,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                Scope = ArtifactDeliveryScope.ArtifactRead.ToString()
            });
        validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<ArtifactDeliveryScope>>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionTokenValidationResult.Success(new ConnectionTokenPrincipal { NodeKey = "runtime" }));
        service.Push("release-1", "api", Arg.Any<CancellationToken>()).Returns(delivery);
        service.GetPendingForPull("runtime-1", Arg.Any<CancellationToken>()).Returns([package]);
        service.GetForPull("runtime-1", "release-1", Arg.Any<CancellationToken>()).Returns(package);
        service.AcknowledgePull("runtime-1", "release-1", "runtime-artifact-1", "Ready", Arg.Any<CancellationToken>())
            .Returns(delivery);
        authenticator.AuthenticateRuntimeNodeAsync(Arg.Any<HttpRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ArtifactDeliveryEndpointAuthenticationResult.Success());

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(service);
                services.AddSingleton(authenticator);
                services.AddSingleton(issuer);
                services.AddSingleton(validator);
            },
            endpoints => endpoints.MapOrchestratorArtifactDeliveryEndpoints());

        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "POST", "/distribution/connect/token", JsonSerializer.Serialize(new ConnectionTokenRequest
        {
            ClientId = "runtime",
            ClientSecret = "secret",
            Scope = "ArtifactRead"
        }))).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "GET", "/distribution/runtime-nodes/{runtimeNodeId}/connect/validate", routeValues: new Dictionary<string, object?>
        {
            ["runtimeNodeId"] = "runtime-1"
        }, bearerToken: "token")).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "POST", "/distribution/release-targets/{releaseTargetId}/push", routeValues: new Dictionary<string, object?>
        {
            ["releaseTargetId"] = "release-1"
        })).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "GET", "/distribution/runtime-nodes/{runtimeNodeId}/artifacts/pending", routeValues: new Dictionary<string, object?>
        {
            ["runtimeNodeId"] = "runtime-1"
        })).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "GET", "/distribution/runtime-nodes/{runtimeNodeId}/artifacts/{releaseTargetId}", routeValues: new Dictionary<string, object?>
        {
            ["runtimeNodeId"] = "runtime-1",
            ["releaseTargetId"] = "release-1"
        })).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "POST", "/distribution/runtime-nodes/{runtimeNodeId}/artifacts/{releaseTargetId}/ack", JsonSerializer.Serialize(new RuntimeArtifactPullAckRequest
        {
            RuntimeArtifactId = "runtime-artifact-1",
            RuntimeArtifactStatus = "Ready"
        }), new Dictionary<string, object?>
        {
            ["runtimeNodeId"] = "runtime-1",
            ["releaseTargetId"] = "release-1"
        })).StatusCode);
    }

    [Fact]
    public async Task ControlPlaneDistributionEndpointsReturnUnauthorizedOrBadRequestOnFailures()
    {
        var service = Substitute.For<IArtifactDeliveryApplicationService>();
        var authenticator = Substitute.For<IArtifactDeliveryEndpointAuthenticator>();
        var issuer = Substitute.For<IControlPlaneConnectionTokenIssuer>();
        var validator = Substitute.For<IControlPlaneConnectionTokenValidator>();

        issuer.IssueAsync(Arg.Any<ConnectionTokenRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ConnectionTokenResponse>(new InvalidOperationException("invalid")));
        validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<ArtifactDeliveryScope>>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionTokenValidationResult.Failure("bad token"));
        service.Push("release-1", "api", Arg.Any<CancellationToken>())
            .Returns(new RuntimeArtifactDeliveryResult { Succeeded = false, Status = "Rejected" });
        service.AcknowledgePull("runtime-1", "release-1", string.Empty, string.Empty, Arg.Any<CancellationToken>())
            .Returns(new RuntimeArtifactDeliveryResult { Succeeded = true, Status = "Acknowledged" });
        authenticator.AuthenticateRuntimeNodeAsync(Arg.Any<HttpRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ArtifactDeliveryEndpointAuthenticationResult.Failure("invalid"));

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(service);
                services.AddSingleton(authenticator);
                services.AddSingleton(issuer);
                services.AddSingleton(validator);
            },
            endpoints => endpoints.MapOrchestratorArtifactDeliveryEndpoints());

        Assert.Equal(StatusCodes.Status400BadRequest, (await Invoke(app, "POST", "/distribution/connect/token", "{}")).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await Invoke(app, "GET", "/distribution/runtime-nodes/{runtimeNodeId}/connect/validate", routeValues: new Dictionary<string, object?>
        {
            ["runtimeNodeId"] = "runtime-1"
        })).StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, (await Invoke(app, "POST", "/distribution/release-targets/{releaseTargetId}/push", routeValues: new Dictionary<string, object?>
        {
            ["releaseTargetId"] = "release-1"
        })).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await Invoke(app, "GET", "/distribution/runtime-nodes/{runtimeNodeId}/artifacts/pending", routeValues: new Dictionary<string, object?>
        {
            ["runtimeNodeId"] = "runtime-1"
        })).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await Invoke(app, "GET", "/distribution/runtime-nodes/{runtimeNodeId}/artifacts/{releaseTargetId}", routeValues: new Dictionary<string, object?>
        {
            ["runtimeNodeId"] = "runtime-1",
            ["releaseTargetId"] = "release-1"
        })).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await Invoke(app, "POST", "/distribution/runtime-nodes/{runtimeNodeId}/artifacts/{releaseTargetId}/ack", routeValues: new Dictionary<string, object?>
        {
            ["runtimeNodeId"] = "runtime-1",
            ["releaseTargetId"] = "release-1"
        })).StatusCode);

        authenticator.AuthenticateRuntimeNodeAsync(Arg.Any<HttpRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ArtifactDeliveryEndpointAuthenticationResult.Success());
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "POST", "/distribution/runtime-nodes/{runtimeNodeId}/artifacts/{releaseTargetId}/ack", "{", new Dictionary<string, object?>
        {
            ["runtimeNodeId"] = "runtime-1",
            ["releaseTargetId"] = "release-1"
        })).StatusCode);
    }

    [Fact]
    public async Task RuntimeDistributionEndpointsHandleTokenDeploySourcesPendingAndApply()
    {
        var issuer = Substitute.For<IRuntimeConnectionTokenIssuer>();
        var validator = Substitute.For<IRuntimeConnectionTokenValidator>();
        var authenticator = Substitute.For<IRuntimeArtifactDeliveryEndpointAuthenticator>();
        var deployment = Substitute.For<IRuntimeArtifactDeploymentService>();
        var pull = Substitute.For<IControlPlaneArtifactPullService>();
        var package = CreatePackage("release-1");
        var deployed = new RuntimeArtifactDeploymentResult
        {
            Accepted = true,
            RuntimeArtifactId = "runtime-artifact-1",
            Status = "Ready"
        };

        issuer.IssueAsync(Arg.Any<ConnectionTokenRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ConnectionTokenResponse { AccessToken = "token", ExpiresIn = 3600 });
        validator.ValidateAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<ArtifactDeliveryScope>>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionTokenValidationResult.Success(new ConnectionTokenPrincipal { NodeKey = "design" }));
        authenticator.AuthenticateAsync(Arg.Any<HttpRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(RuntimeArtifactDeliveryEndpointAuthenticationResult.Success("design"));
        deployment.DeployAsync(Arg.Any<RuntimeArtifactDeliveryPackage>(), "design", Arg.Any<CancellationToken>())
            .Returns(deployed);
        pull.GetSources().Returns([new ControlPlaneDistributionSource { Key = "design", Name = "Design", EndpointBaseUri = "https://design" }]);
        pull.GetPendingAsync("design", Arg.Any<CancellationToken>()).Returns([package]);
        pull.ApplyAsync("design", "release-1", Arg.Any<CancellationToken>()).Returns(deployed);

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(issuer);
                services.AddSingleton(validator);
                services.AddSingleton(authenticator);
                services.AddSingleton(deployment);
                services.AddSingleton(pull);
            },
            endpoints => endpoints.MapOrchestratorRuntimeDistributionEndpoints());

        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "POST", "/runtime/distribution/connect/token", "{}")).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "GET", "/runtime/distribution/connect/validate", bearerToken: "token")).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "POST", "/runtime/artifacts/deploy", JsonSerializer.Serialize(package))).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "GET", "/runtime/distribution/control-planes")).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "GET", "/runtime/distribution/control-planes/{sourceKey}/artifacts/pending", routeValues: new Dictionary<string, object?>
        {
            ["sourceKey"] = "design"
        })).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Invoke(app, "POST", "/runtime/distribution/control-planes/{sourceKey}/artifacts/{releaseTargetId}/apply", routeValues: new Dictionary<string, object?>
        {
            ["sourceKey"] = "design",
            ["releaseTargetId"] = "release-1"
        })).StatusCode);
    }

    [Fact]
    public async Task RuntimeDistributionEndpointsReturnUnauthorizedOrBadRequestOnFailures()
    {
        var issuer = Substitute.For<IRuntimeConnectionTokenIssuer>();
        var validator = Substitute.For<IRuntimeConnectionTokenValidator>();
        var authenticator = Substitute.For<IRuntimeArtifactDeliveryEndpointAuthenticator>();
        var deployment = Substitute.For<IRuntimeArtifactDeploymentService>();
        var pull = Substitute.For<IControlPlaneArtifactPullService>();

        issuer.IssueAsync(Arg.Any<ConnectionTokenRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ConnectionTokenResponse>(new InvalidOperationException("invalid")));
        validator.ValidateAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<ArtifactDeliveryScope>>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionTokenValidationResult.Failure("bad token"));
        authenticator.AuthenticateAsync(Arg.Any<HttpRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(RuntimeArtifactDeliveryEndpointAuthenticationResult.Failure("bad signature"));

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(issuer);
                services.AddSingleton(validator);
                services.AddSingleton(authenticator);
                services.AddSingleton(deployment);
                services.AddSingleton(pull);
            },
            endpoints => endpoints.MapOrchestratorRuntimeDistributionEndpoints());

        Assert.Equal(StatusCodes.Status400BadRequest, (await Invoke(app, "POST", "/runtime/distribution/connect/token", "{}")).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await Invoke(app, "GET", "/runtime/distribution/connect/validate")).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await Invoke(app, "POST", "/runtime/artifacts/deploy", JsonSerializer.Serialize(CreatePackage("release-1")))).StatusCode);

        authenticator.AuthenticateAsync(Arg.Any<HttpRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(RuntimeArtifactDeliveryEndpointAuthenticationResult.Success("design"));
        Assert.Equal(StatusCodes.Status400BadRequest, (await Invoke(app, "POST", "/runtime/artifacts/deploy", "")).StatusCode);
    }

    private static WebApplication BuildApp(Action<IServiceCollection> configureServices, Action<IEndpointRouteBuilder> map)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        configureServices(builder.Services);
        var app = builder.Build();
        map(app);
        return app;
    }

    private static async Task<EndpointInvocationResult> Invoke(
        WebApplication app,
        string method,
        string routePattern,
        string body = "",
        Dictionary<string, object?>? routeValues = null,
        string bearerToken = "")
    {
        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(x => string.Equals(x.RoutePattern.RawText, routePattern, StringComparison.Ordinal));
        var httpContext = new DefaultHttpContext
        {
            RequestServices = app.Services
        };
        httpContext.SetEndpoint(endpoint);
        httpContext.Request.Method = method;
        httpContext.Request.Path = routePattern.Replace("{", string.Empty, StringComparison.Ordinal).Replace("}", string.Empty, StringComparison.Ordinal);
        httpContext.Response.Body = new MemoryStream();

        foreach (var pair in routeValues ?? [])
        {
            httpContext.Request.RouteValues[pair.Key] = pair.Value;
        }

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            httpContext.Request.Headers.Authorization = $"Bearer {bearerToken}";
        }

        if (body.Length > 0)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            httpContext.Request.ContentType = "application/json";
            httpContext.Request.ContentLength = bytes.Length;
            httpContext.Request.Body = new MemoryStream(bytes);
        }

        await endpoint.RequestDelegate!(httpContext);
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);
        return new EndpointInvocationResult(httpContext.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static RuntimeArtifactDeliveryPackage CreatePackage(string releaseTargetId)
        => new()
        {
            ReleaseTargetId = releaseTargetId,
            ArtifactId = "artifact-1",
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0.0",
            OrchestrationDefinitionId = "definition-1",
            OrchestrationVersionId = "version-1",
            OrchestrationDefinitionKey = "sales.sale.created",
            Version = "1.0.0",
            Checksum = "checksum",
            PayloadJson = "{}",
            CorrelationId = "correlation",
            PromotedBy = "tester",
            PromotedOnUtc = DateTime.UtcNow
        };

    private sealed record EndpointInvocationResult(int StatusCode, string Body);
}
