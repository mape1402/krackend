using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CPDesign = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using CPDistribution = Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Api;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.Runtime.Api;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests;

public sealed class RestApiEndpointRouteBuilderTests
{
    [Fact]
    public async Task ControlPlaneApiMapsHealthAndDesignQueries()
    {
        var orchestrationService = Substitute.For<CPDesign.IOrchestrationApplicationService>();
        orchestrationService.GetAll(Arg.Any<CPDesign.GetOrchestrationDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new CPDesign.ApplicationPagedResult<CPDesign.OrchestrationDefinitionModel>(
                2,
                4,
                7,
                3,
                [new CPDesign.OrchestrationDefinitionModel { Id = "orch-1", Key = "sales.sale.created", Name = "Sale Created" }]));

        await using var app = BuildApp(
            services => services.AddSingleton(orchestrationService),
            endpoints => endpoints.MapKrackendOrchestrationsControlPlaneApi(options =>
            {
                options.DefaultPageSize = 10;
                options.MaxPageSize = 50;
            }));

        var health = await Invoke(app, "GET", "/api/v1/control-plane/health");
        var orchestrations = await Invoke(app, "GET", "/api/v1/control-plane/design/orchestrations", queryString: "?pageNumber=2&pageSize=3");

        Assert.Equal(StatusCodes.Status200OK, health.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, orchestrations.StatusCode);
        Assert.Contains("sales.sale.created", orchestrations.Body, StringComparison.Ordinal);

        await orchestrationService.Received(1).GetAll(
            Arg.Is<CPDesign.GetOrchestrationDefinitionsQuery>(x =>
                x.Settings.PageNumber == 2 &&
                x.Settings.PageSize == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ControlPlaneApiMapsDistributionPushAndNormalizesActor()
    {
        var deliveryService = Substitute.For<CPDistribution.IArtifactDeliveryApplicationService>();
        deliveryService.Push("target-1", "api", Arg.Any<CancellationToken>())
            .Returns(new RuntimeArtifactDeliveryResult
            {
                Succeeded = true,
                ReleaseTargetId = "target-1",
                RuntimeNodeId = "runtime-1",
                ArtifactId = "artifact-1",
                Status = "Delivered"
            });

        await using var app = BuildApp(
            services => services.AddSingleton(deliveryService),
            endpoints => endpoints.MapKrackendOrchestrationsControlPlaneApi());

        var result = await Invoke(
            app,
            "POST",
            "/api/v1/control-plane/distribution/release-targets/{releaseTargetId}/push",
            JsonSerializer.Serialize(new ActorRequest()),
            new Dictionary<string, object?> { ["releaseTargetId"] = "target-1" });

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Contains("Delivered", result.Body, StringComparison.Ordinal);
        await deliveryService.Received(1).Push("target-1", "api", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ControlPlaneApiMapsRuntimeNodeLifecycleEndpoints()
    {
        var nodeService = Substitute.For<CPDistribution.IRuntimeNodeApplicationService>();
        nodeService.GetAll(Arg.Any<CPDistribution.ApplicationPagedSettings>(), Arg.Any<CancellationToken>())
            .Returns(new CPDistribution.ApplicationPagedResult<CPDistribution.RuntimeNodeModel>
            {
                PageNumber = 1,
                PageSize = 5,
                TotalRows = 1,
                TotalPages = 1,
                Rows =
                [
                    new CPDistribution.RuntimeNodeModel
                    {
                        Id = "runtime-1",
                        Name = "Local Runtime",
                        Code = "local-runtime",
                        DistributionMode = DistributionMode.HybridSync.ToString(),
                        Status = RuntimeNodeStatus.Enabled.ToString()
                    }
                ]
            });

        await using var app = BuildApp(
            services => services.AddSingleton(nodeService),
            endpoints => endpoints.MapKrackendOrchestrationsControlPlaneApi());

        var list = await Invoke(app, "GET", "/api/v1/control-plane/distribution/runtime-nodes", queryString: "?pageNumber=1&pageSize=5");
        Assert.Equal(StatusCodes.Status200OK, list.StatusCode);
        Assert.Contains("local-runtime", list.Body, StringComparison.Ordinal);
        await nodeService.Received(1).GetAll(
            Arg.Is<CPDistribution.ApplicationPagedSettings>(x => x.PageNumber == 1 && x.PageSize == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RuntimeApiMapsDiagnosticsAndArtifactStandup()
    {
        var diagnostics = Substitute.For<IRuntimeDiagnosticsReader>();
        var artifactRepository = Substitute.For<IRuntimeArtifactRepository>();
        var notifier = Substitute.For<IRuntimeArtifactReadyNotifier>();
        var artifact = CreateArtifact(RuntimeOrchestrationArtifactStatus.Ready, isActive: true);

        diagnostics.GetSummary(Arg.Any<CancellationToken>())
            .Returns(new RuntimeDashboardSummaryModel(
                new RuntimeSummaryModel(1, 2, 3, 4, 5, 6, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(-1)),
                [new TrafficPointModel(DateTime.UtcNow, 1, 2, 3, 4)],
                []));
        artifactRepository.GetAll(Arg.Any<CancellationToken>()).Returns([artifact]);
        artifactRepository.GetById(artifact.Id, Arg.Any<CancellationToken>()).Returns(artifact);

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(diagnostics);
                services.AddSingleton(artifactRepository);
                services.AddSingleton(notifier);
            },
            endpoints => endpoints.MapKrackendOrchestrationsRuntimeApi());

        var summary = await Invoke(app, "GET", "/api/v1/runtime/instances/summary");
        var artifacts = await Invoke(app, "GET", "/api/v1/runtime/artifacts", queryString: "?search=sales");
        var standup = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/artifacts/{artifactId}/standup",
            routeValues: new Dictionary<string, object?> { ["artifactId"] = artifact.Id.ToString() });

        Assert.Equal(StatusCodes.Status200OK, summary.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, artifacts.StatusCode);
        Assert.Contains("sales.sale.created", artifacts.Body, StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status202Accepted, standup.StatusCode);

        await notifier.Received(1).NotifyReadyAsync(
            Arg.Is<RuntimeArtifactReadyGossipMessage>(x =>
                x.ArtifactId == artifact.Id.ToString() &&
                x.OrchestrationDefinitionKey == artifact.OrchestrationDefinitionKey &&
                x.Version == artifact.Version.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RuntimeApiRejectsInvalidOrInactiveArtifactStandup()
    {
        var artifactRepository = Substitute.For<IRuntimeArtifactRepository>();
        var notifier = Substitute.For<IRuntimeArtifactReadyNotifier>();
        var artifact = CreateArtifact(RuntimeOrchestrationArtifactStatus.Pending, isActive: true);

        artifactRepository.GetById(artifact.Id, Arg.Any<CancellationToken>()).Returns(artifact);

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(artifactRepository);
                services.AddSingleton(notifier);
            },
            endpoints => endpoints.MapKrackendOrchestrationsRuntimeApi());

        var invalidId = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/artifacts/{artifactId}/standup",
            routeValues: new Dictionary<string, object?> { ["artifactId"] = "bad-id" });
        var pending = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/artifacts/{artifactId}/standup",
            routeValues: new Dictionary<string, object?> { ["artifactId"] = artifact.Id.ToString() });

        Assert.Equal(StatusCodes.Status400BadRequest, invalidId.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, pending.StatusCode);
        await notifier.DidNotReceiveWithAnyArgs().NotifyReadyAsync(default!, default);
    }

    [Fact]
    public async Task RuntimeApiMapsIngressesAndDesignNodes()
    {
        var ingressRepository = Substitute.For<IRuntimeIngressConfigurationRepository>();
        var designNodeRepository = Substitute.For<IRuntimeDesignNodeRepository>();
        var artifactId = Id.New();
        var ingress = new RuntimeIngressConfiguration
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = artifactId,
            ConfigurationKey = "sales.sale.created:trigger",
            IngressKind = IngressKind.Trigger,
            IngressTransport = IngressTransport.Messaging,
            SettingsPayload = "{}",
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };
        var node = new RuntimeDesignNode
        {
            Id = Id.New(),
            Key = "design",
            Name = "Design",
            DistributionMode = DistributionConnectionMode.HybridSync,
            Status = RuntimeDesignNodeStatus.Enabled,
            IsEnabled = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        ingressRepository.GetActiveByArtifactIdAsync(artifactId, Arg.Any<CancellationToken>()).Returns([ingress]);
        designNodeRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([node]);

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(ingressRepository);
                services.AddSingleton(designNodeRepository);
            },
            endpoints => endpoints.MapKrackendOrchestrationsRuntimeApi());

        var ingresses = await Invoke(app, "GET", "/api/v1/runtime/ingresses", queryString: $"?artifactId={artifactId}");
        var designNodes = await Invoke(app, "GET", "/api/v1/runtime/design-nodes");

        Assert.Equal(StatusCodes.Status200OK, ingresses.StatusCode);
        Assert.Contains("sales.sale.created:trigger", ingresses.Body, StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status200OK, designNodes.StatusCode);
        Assert.Contains("design", designNodes.Body, StringComparison.Ordinal);
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
        string queryString = "")
    {
        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(x =>
                string.Equals(x.RoutePattern.RawText, routePattern, StringComparison.Ordinal) &&
                (x.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase) ?? true));
        var httpContext = new DefaultHttpContext
        {
            RequestServices = app.Services
        };
        httpContext.SetEndpoint(endpoint);
        httpContext.Request.Method = method;
        httpContext.Request.Path = routePattern.Replace("{", string.Empty, StringComparison.Ordinal).Replace("}", string.Empty, StringComparison.Ordinal);
        httpContext.Request.QueryString = new QueryString(queryString);
        httpContext.Response.Body = new MemoryStream();

        foreach (var pair in routeValues ?? [])
        {
            httpContext.Request.RouteValues[pair.Key] = pair.Value;
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

    private static RuntimeOrchestrationArtifact CreateArtifact(RuntimeOrchestrationArtifactStatus status, bool isActive)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum("checksum"),
            ArtifactPayload = JsonNode.Parse("{}")!,
            Status = status,
            IsActive = isActive,
            IngressGeneration = 3,
            DeployedOnUtc = DateTime.UtcNow,
            ActivatedOnUtc = DateTime.UtcNow
        };

    private sealed record EndpointInvocationResult(int StatusCode, string Body);
}
