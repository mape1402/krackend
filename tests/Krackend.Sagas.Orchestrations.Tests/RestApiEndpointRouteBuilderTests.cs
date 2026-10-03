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
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
        var metadataService = Substitute.For<CPDesign.IMetadataDescriptorApplicationService>();
        orchestrationService.GetAll(Arg.Any<CPDesign.GetOrchestrationDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new CPDesign.ApplicationPagedResult<CPDesign.OrchestrationDefinitionModel>(
                2,
                4,
                7,
                3,
                [new CPDesign.OrchestrationDefinitionModel { Id = "orch-1", Key = "sales.sale.created", Name = "Sale Created" }]));
        metadataService.GetAll(Arg.Any<CPDesign.GetMetadataDescriptorsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new CPDesign.ApplicationPagedResult<CPDesign.MetadataDescriptorModel>(
                1,
                1,
                1,
                10,
                [new CPDesign.MetadataDescriptorModel { Id = "meta-1", Key = "audit", DisplayName = "Audit" }]));

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(orchestrationService);
                services.AddSingleton(metadataService);
            },
            endpoints => endpoints.MapKrackendOrchestrationsControlPlaneApi(options =>
            {
                options.DefaultPageSize = 10;
                options.MaxPageSize = 50;
            }));

        var health = await Invoke(app, "GET", "/api/v1/control-plane/health");
        var orchestrations = await Invoke(app, "GET", "/api/v1/control-plane/design/orchestrations", queryString: "?pageNumber=2&pageSize=3");
        var metadata = await Invoke(app, "GET", "/api/v1/control-plane/design/metadata", queryString: "?pageNumber=1&pageSize=10&searchText=aud");
        var deleteMetadata = await Invoke(
            app,
            "DELETE",
            "/api/v1/control-plane/design/metadata/{metadataDescriptorId}",
            routeValues: new Dictionary<string, object?> { ["metadataDescriptorId"] = "01K6AW24N95NS6G3W7CNZQK4S2" });

        Assert.Equal(StatusCodes.Status200OK, health.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, orchestrations.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, metadata.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, deleteMetadata.StatusCode);
        Assert.Contains("sales.sale.created", orchestrations.Body, StringComparison.Ordinal);
        Assert.Contains("audit", metadata.Body, StringComparison.Ordinal);

        await orchestrationService.Received(1).GetAll(
            Arg.Is<CPDesign.GetOrchestrationDefinitionsQuery>(x =>
                x.Settings.PageNumber == 2 &&
                x.Settings.PageSize == 3),
            Arg.Any<CancellationToken>());
        await metadataService.Received(1).GetAll(
            Arg.Is<CPDesign.GetMetadataDescriptorsQuery>(x =>
                x.PagedSettings.PageNumber == 1 &&
                x.PagedSettings.PageSize == 10 &&
                x.SearchText == "aud"),
            Arg.Any<CancellationToken>());
        await metadataService.Received(1).Delete(
            Arg.Is<CPDesign.DeleteMetadataDescriptorCommand>(x => x.MetadataDescriptorId == "01K6AW24N95NS6G3W7CNZQK4S2"),
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
        var artifactDetail = await Invoke(
            app,
            "GET",
            "/api/v1/runtime/artifacts/{artifactId}",
            routeValues: new Dictionary<string, object?> { ["artifactId"] = artifact.Id.ToString() });
        var invalidArtifactDetail = await Invoke(
            app,
            "GET",
            "/api/v1/runtime/artifacts/{artifactId}",
            routeValues: new Dictionary<string, object?> { ["artifactId"] = "not-an-id" });
        var standup = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/artifacts/{artifactId}/standup",
            routeValues: new Dictionary<string, object?> { ["artifactId"] = artifact.Id.ToString() });

        Assert.Equal(StatusCodes.Status200OK, summary.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, artifacts.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, artifactDetail.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, invalidArtifactDetail.StatusCode);
        Assert.Contains("sales.sale.created", artifacts.Body, StringComparison.Ordinal);
        Assert.Contains("sales.sale.created", artifactDetail.Body, StringComparison.Ordinal);
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
    public async Task RuntimeApiMapsReplayAndAbortRecoveryEndpoints()
    {
        var recovery = Substitute.For<IOrchestrationRecoveryService>();
        var replayPayload = string.Empty;
        var abortReason = string.Empty;
        recovery.ReplayAsync("instance-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                replayPayload = call.ArgAt<string>(1);
                return OrchestrationRecoveryResult.Success("instance-1", "Running", "Replay requested.");
            });
        recovery.AbortAsync("instance-2", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                abortReason = call.ArgAt<string>(1);
                return OrchestrationRecoveryResult.Success("instance-2", "Aborted", "Operator cancelled.");
            });

        await using var app = BuildApp(
            services => services.AddSingleton(recovery),
            endpoints => endpoints.MapKrackendOrchestrationsRuntimeApi());

        var replay = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/instances/{instanceId}/replay",
            "{\"payload\":\"{\\\"fixed\\\":true}\"}",
            new Dictionary<string, object?> { ["instanceId"] = "instance-1" });
        var abort = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/instances/{instanceId}/abort",
            "{\"reason\":\"Operator cancelled.\"}",
            new Dictionary<string, object?> { ["instanceId"] = "instance-2" });

        Assert.Equal(StatusCodes.Status200OK, replay.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, abort.StatusCode);
        Assert.Contains("Running", replay.Body, StringComparison.Ordinal);
        Assert.Contains("Aborted", abort.Body, StringComparison.Ordinal);
        Assert.Equal("{\"fixed\":true}", replayPayload);
        Assert.Equal("Operator cancelled.", abortReason);

        await recovery.Received(1).ReplayAsync("instance-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await recovery.Received(1).AbortAsync("instance-2", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RuntimeApiMapsSnapshotDetailAndRejectedRecoveryResults()
    {
        var diagnostics = Substitute.For<IRuntimeDiagnosticsReader>();
        var recovery = Substitute.For<IOrchestrationRecoveryService>();
        var now = DateTime.UtcNow;
        var row = new InstanceRowModel(
            "instance-1",
            "sales.sale.created",
            "1.0.0",
            "corr-1",
            "saga-1",
            "exec-1",
            "Running",
            "od-status-active",
            "stage-1",
            "task-1",
            now.AddMinutes(-5),
            now,
            null,
            null,
            null,
            string.Empty);
        var summary = new RuntimeSummaryModel(1, 0, 0, 0, 0, 0, now.AddMinutes(-1), now.AddHours(-1));

        diagnostics.GetSnapshot(Arg.Any<CancellationToken>())
            .Returns(new RuntimeDashboardSnapshotModel(summary, [row], [], []));
        diagnostics.GetDetail("instance-1", Arg.Any<CancellationToken>())
            .Returns(new InstanceDetailModel(row, [], [], [], "{}", "{}", [], [], []));
        recovery.ReplayAsync("instance-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OrchestrationRecoveryResult.Rejected("instance-1", "Completed", "Already terminal."));
        recovery.AbortAsync("instance-2", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OrchestrationRecoveryResult.Rejected("instance-2", "Running", "Abort denied."));

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(diagnostics);
                services.AddSingleton(recovery);
            },
            endpoints => endpoints.MapKrackendOrchestrationsRuntimeApi());

        var snapshot = await Invoke(app, "GET", "/api/v1/runtime/instances/snapshot");
        var detail = await Invoke(
            app,
            "GET",
            "/api/v1/runtime/instances/{instanceId}",
            routeValues: new Dictionary<string, object?> { ["instanceId"] = "instance-1" });
        var missingInstanceId = await Invoke(
            app,
            "GET",
            "/api/v1/runtime/instances/{instanceId}",
            routeValues: new Dictionary<string, object?> { ["instanceId"] = " " });
        var rejectedReplay = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/instances/{instanceId}/replay",
            "{\"payload\":\"{}\"}",
            new Dictionary<string, object?> { ["instanceId"] = "instance-1" });
        var rejectedAbort = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/instances/{instanceId}/abort",
            "{\"reason\":\"nope\"}",
            new Dictionary<string, object?> { ["instanceId"] = "instance-2" });

        Assert.Equal(StatusCodes.Status200OK, snapshot.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, detail.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, missingInstanceId.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, rejectedReplay.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, rejectedAbort.StatusCode);
        Assert.Contains("instance-1", snapshot.Body, StringComparison.Ordinal);
        Assert.Contains("Already terminal.", rejectedReplay.Body, StringComparison.Ordinal);
        Assert.Contains("Abort denied.", rejectedAbort.Body, StringComparison.Ordinal);
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
        ingressRepository.ReadActiveAsync(0, 5, Arg.Any<CancellationToken>()).Returns([ingress]);
        designNodeRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([node]);
        designNodeRepository.GetByIdAsync(node.Id, Arg.Any<CancellationToken>()).Returns(node);

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(ingressRepository);
                services.AddSingleton(designNodeRepository);
            },
            endpoints => endpoints.MapKrackendOrchestrationsRuntimeApi(options =>
            {
                options.DefaultPageSize = 3;
                options.MaxPageSize = 5;
            }));

        var ingresses = await Invoke(app, "GET", "/api/v1/runtime/ingresses", queryString: $"?artifactId={artifactId}");
        var pagedIngresses = await Invoke(app, "GET", "/api/v1/runtime/ingresses", queryString: "?skip=-5&take=999");
        var invalidIngressArtifact = await Invoke(app, "GET", "/api/v1/runtime/ingresses", queryString: "?artifactId=bad-id");
        var designNodes = await Invoke(app, "GET", "/api/v1/runtime/design-nodes");
        var designNode = await Invoke(
            app,
            "GET",
            "/api/v1/runtime/design-nodes/{designNodeId}",
            routeValues: new Dictionary<string, object?> { ["designNodeId"] = node.Id.ToString() });
        var invalidDesignNode = await Invoke(
            app,
            "GET",
            "/api/v1/runtime/design-nodes/{designNodeId}",
            routeValues: new Dictionary<string, object?> { ["designNodeId"] = "bad-id" });

        Assert.Equal(StatusCodes.Status200OK, ingresses.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, pagedIngresses.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, invalidIngressArtifact.StatusCode);
        Assert.Contains("sales.sale.created:trigger", ingresses.Body, StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status200OK, designNodes.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, designNode.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, invalidDesignNode.StatusCode);
        Assert.Contains("design", designNodes.Body, StringComparison.Ordinal);
        Assert.Contains("Design", designNode.Body, StringComparison.Ordinal);
        await ingressRepository.Received(1).ReadActiveAsync(0, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RuntimeApiMapsDesignNodeWriteCredentialAndManualPullEndpoints()
    {
        var designNodeRepository = Substitute.For<IRuntimeDesignNodeRepository>();
        var connectionService = Substitute.For<IRuntimeDesignNodeConnectionService>();
        var pullService = Substitute.For<IControlPlaneArtifactPullService>();
        var designNodeId = Id.New();
        RuntimeDesignNode? createdNode = null;
        RuntimeDesignNode? updatedNode = null;

        designNodeRepository.UpsertAsync(Arg.Do<RuntimeDesignNode>(node =>
        {
            if (node.Id == designNodeId)
            {
                updatedNode = node;
            }
            else
            {
                createdNode = node;
            }
        }), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        connectionService.GenerateCredentialPackageAsync(designNodeId.ToString(), "http://runtime", Arg.Any<CancellationToken>())
            .Returns(new RuntimeDesignNodeCredentialPackageModel { Json = "{\"clientId\":\"runtime\"}", Base64 = "abc123" });
        connectionService.ValidateConnectionAsync(designNodeId.ToString(), Arg.Any<CancellationToken>())
            .Returns(new RuntimeDesignNodeConnectionValidationModel { Succeeded = true, Message = "Connected" });
        pullService.GetSources().Returns(
        [
            new ControlPlaneDistributionSource
            {
                Key = "cp",
                Name = "Control Plane",
                EndpointBaseUri = "http://control-plane",
                RemoteRuntimeNodeId = "runtime-1",
                ClientId = "client",
                ProtectedSecret = "secret",
                KeyId = "key",
                RequestedScopes = "artifact:read"
            }
        ]);
        pullService.GetPendingAsync("cp", Arg.Any<CancellationToken>()).Returns(
        [
            new RuntimeArtifactDeliveryPackage
            {
                ReleaseTargetId = "target-1",
                ArtifactId = "artifact-1",
                ArtifactType = "orchestration-version-snapshot",
                SchemaVersion = "1",
                OrchestrationDefinitionId = "definition-1",
                OrchestrationVersionId = "version-1",
                OrchestrationDefinitionKey = "sales.sale.created",
                Version = "1.0.0",
                Checksum = "checksum",
                PayloadJson = "{}",
                CorrelationId = "corr-1",
                PromotedBy = "api",
                PromotedOnUtc = DateTime.UtcNow
            }
        ]);
        pullService.ApplyAsync("cp", "target-1", Arg.Any<CancellationToken>())
            .Returns(new RuntimeArtifactDeploymentResult
            {
                Accepted = true,
                RuntimeArtifactId = "runtime-artifact-1",
                Status = "Ready",
                Message = "Applied"
            });

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(designNodeRepository);
                services.AddSingleton(connectionService);
                services.AddSingleton(pullService);
            },
            endpoints => endpoints.MapKrackendOrchestrationsRuntimeApi());

        var createBody = JsonSerializer.Serialize(new UpsertRuntimeDesignNodeRequest(
            string.Empty,
            " design ",
            " Design Node ",
            " http://runtime/ ",
            " runtime-1 ",
            DistributionConnectionMode.HybridSync,
            0,
            0,
            0,
            " sample "));
        var updateBody = JsonSerializer.Serialize(new UpsertRuntimeDesignNodeRequest(
            designNodeId.ToString(),
            "design-updated",
            "Design Updated",
            "http://runtime-updated/",
            "runtime-2",
            DistributionConnectionMode.RuntimeFetchesFromDesign,
            120,
            20,
            30,
            "updated"));

        var created = await Invoke(app, "POST", "/api/v1/runtime/design-nodes", createBody);
        var updated = await Invoke(
            app,
            "PUT",
            "/api/v1/runtime/design-nodes/{designNodeId}",
            updateBody,
            new Dictionary<string, object?> { ["designNodeId"] = designNodeId.ToString() });
        var invalidUpdate = await Invoke(
            app,
            "PUT",
            "/api/v1/runtime/design-nodes/{designNodeId}",
            updateBody,
            new Dictionary<string, object?> { ["designNodeId"] = "bad-id" });
        var status = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/design-nodes/{designNodeId}/status",
            JsonSerializer.Serialize(new RuntimeDesignNodeStatusRequest(RuntimeDesignNodeStatus.Enabled)),
            new Dictionary<string, object?> { ["designNodeId"] = designNodeId.ToString() });
        var invalidStatus = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/design-nodes/{designNodeId}/status",
            JsonSerializer.Serialize(new RuntimeDesignNodeStatusRequest(RuntimeDesignNodeStatus.Enabled)),
            new Dictionary<string, object?> { ["designNodeId"] = "bad-id" });
        var enabled = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/design-nodes/{designNodeId}/enabled",
            JsonSerializer.Serialize(new RuntimeDesignNodeEnabledRequest(false)),
            new Dictionary<string, object?> { ["designNodeId"] = designNodeId.ToString() });
        var invalidEnabled = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/design-nodes/{designNodeId}/enabled",
            JsonSerializer.Serialize(new RuntimeDesignNodeEnabledRequest(false)),
            new Dictionary<string, object?> { ["designNodeId"] = "bad-id" });
        var generated = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/design-nodes/{designNodeId}/credentials/generate",
            JsonSerializer.Serialize(new Krackend.Sagas.Orchestrations.Runtime.Api.CredentialIssuerRequest("http://runtime")),
            new Dictionary<string, object?> { ["designNodeId"] = designNodeId.ToString() });
        var imported = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/design-nodes/credentials/import",
            JsonSerializer.Serialize(new ImportRuntimeDesignNodeCredentialPackageInput
            {
                DesignNodeId = designNodeId.ToString(),
                Package = "abc123"
            }));
        var validation = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/design-nodes/{designNodeId}/validate",
            routeValues: new Dictionary<string, object?> { ["designNodeId"] = designNodeId.ToString() });
        var sources = await Invoke(app, "GET", "/api/v1/runtime/control-planes");
        var pending = await Invoke(
            app,
            "GET",
            "/api/v1/runtime/control-planes/{sourceKey}/artifacts/pending",
            routeValues: new Dictionary<string, object?> { ["sourceKey"] = "cp" });
        var applied = await Invoke(
            app,
            "POST",
            "/api/v1/runtime/control-planes/{sourceKey}/artifacts/{releaseTargetId}/apply",
            routeValues: new Dictionary<string, object?> { ["sourceKey"] = "cp", ["releaseTargetId"] = "target-1" });

        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, updated.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, invalidUpdate.StatusCode);
        Assert.Equal(StatusCodes.Status204NoContent, status.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, invalidStatus.StatusCode);
        Assert.Equal(StatusCodes.Status204NoContent, enabled.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, invalidEnabled.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, generated.StatusCode);
        Assert.Equal(StatusCodes.Status204NoContent, imported.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, validation.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, sources.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, pending.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, applied.StatusCode);
        Assert.NotNull(createdNode);
        Assert.Equal("design", createdNode.Key);
        Assert.Equal("Design Node", createdNode.Name);
        Assert.Equal("http://runtime", createdNode.EndpointBaseUri);
        Assert.Equal("runtime-1", createdNode.RemoteRuntimeNodeId);
        Assert.Equal(86_400, createdNode.AccessTokenTtlSeconds);
        Assert.Equal(300, createdNode.TokenRefreshSkewSeconds);
        Assert.Equal(300, createdNode.TokenValidationCacheTtlSeconds);
        Assert.NotNull(updatedNode);
        Assert.Equal(designNodeId, updatedNode.Id);
        Assert.Equal("design-updated", updatedNode.Key);
        Assert.Contains("abc123", generated.Body, StringComparison.Ordinal);
        Assert.Contains("Connected", validation.Body, StringComparison.Ordinal);
        Assert.Contains("Control Plane", sources.Body, StringComparison.Ordinal);
        Assert.Contains("sales.sale.created", pending.Body, StringComparison.Ordinal);
        Assert.Contains("Applied", applied.Body, StringComparison.Ordinal);

        await designNodeRepository.Received(1).SetStatusAsync(designNodeId, RuntimeDesignNodeStatus.Enabled, Arg.Any<CancellationToken>());
        await designNodeRepository.Received(1).SetEnabledAsync(designNodeId, false, Arg.Any<CancellationToken>());
        await connectionService.Received(1).ImportCredentialPackageAsync(
            Arg.Is<ImportRuntimeDesignNodeCredentialPackageInput>(x =>
                x.DesignNodeId == designNodeId.ToString() &&
                x.Package == "abc123"),
            Arg.Any<CancellationToken>());
        await pullService.Received(1).GetPendingAsync("cp", Arg.Any<CancellationToken>());
        await pullService.Received(1).ApplyAsync("cp", "target-1", Arg.Any<CancellationToken>());
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
            httpContext.Features.Set<IHttpRequestBodyDetectionFeature>(
                new TestHttpRequestBodyDetectionFeature(canHaveBody: true));
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

    private sealed class TestHttpRequestBodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }
}
