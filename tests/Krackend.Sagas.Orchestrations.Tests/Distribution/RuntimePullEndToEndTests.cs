namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

using System.Net.Http.Json;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sieve.Services;

public sealed class RuntimePullEndToEndTests
{
    [Fact]
    public async Task PullOverHttpInstallsArtifactAndAcknowledgesControlPlane()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var sieve = Substitute.For<ISieveProcessor>();
        var environmentId = Id.New();
        db.DistributionEnvironments.Add(new DistributionEnvironmentEntity { Id = environmentId, Name = "Testing", Code = "test", Description = "", IsEnabled = true });
        await db.SaveChangesAsync();

        var nodes = new RuntimeNodeRepository(db, sieve);
        var nodeId = Id.New();
        await nodes.Create(new RuntimeNode { Id = nodeId, Name = "Runtime", Code = "runtime", EnvironmentId = environmentId,
            DistributionMode = DistributionMode.HybridSync, Status = RuntimeNodeStatus.Enabled, IsEnabled = true,
            Description = "", RegisteredAtUtc = DateTime.UtcNow });
        var artifacts = new ArtifactRepository(db, sieve);
        var versionId = Id.New();
        var artifactId = Id.New();
        var version = new SemanticVersion(1, 0, 0);
        var definitionId = Id.New();
        var schema = new SchemaBindingArtifact(Id.New(), ElementType.Task, Id.New(), Id.New(), "test.sales", version, Id.New(), false)
        {
            IsValidationEnabled = false,
            ContractKind = SchemaContractKind.CommandRequest
        };
        var trigger = new TriggerBindingArtifact(Id.New(), TriggerType.Event,
            new EventTriggerChannelArtifact(schema, "events.test.sales", version), true);
        var payload = new OrchestrationArtifact(definitionId, versionId, "test.sales", "Test sales", "sales", version,
            new Checksum("checksum-a"), [trigger], [], []);
        await artifacts.Create(new Artifact { Id = artifactId, OrchestrationDefinitionId = definitionId.ToString(),
            OrchestrationVersionId = versionId.ToString(), OrchestrationDisplayName = "Test sales", VersionLabel = "Test",
            VersionNumber = version.ToString(), ArtifactType = "orchestration-version.new", SchemaVersion = "1",
            Payload = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Metadata = "{}",
            SourceEvent = "test", SourceVersion = "1", Checksum = "checksum-a", CreatedAtUtc = DateTime.UtcNow });
        var releases = new ReleaseRepository(db, sieve);
        var releaseId = Id.New();
        await releases.Create(new Release { Id = releaseId, ArtifactId = artifactId, OrchestrationDefinitionId = definitionId.ToString(),
            RequestedBy = "e2e", Strategy = "Direct", Status = ReleaseStatus.InProgress, CreatedAtUtc = DateTime.UtcNow }, []);
        var targets = new ReleaseTargetRepository(db, sieve);
        var targetId = Id.New();
        await targets.Create(new ReleaseTarget { Id = targetId, RuntimeNodeId = nodeId, ArtifactId = artifactId,
            ReleaseId = releaseId,
            Status = ReleaseTargetStatus.Pending, ActivationStatus = ActivationStatus.NotActivated,
            AssignedAtUtc = DateTime.UtcNow, CorrelationId = "e2e-correlation", RolloutGroup = "Direct" });

        var delivery = new ArtifactDeliveryApplicationService(new HttpClient(), targets, releases,
            artifacts, nodes, Substitute.For<IRuntimeAccessTokenProvider>());
        var authenticator = Substitute.For<IArtifactDeliveryEndpointAuthenticator>();
        authenticator.AuthenticateRuntimeNodeAsync(Arg.Any<HttpRequest>(), nodeId.ToString(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ArtifactDeliveryEndpointAuthenticationResult.Success());
        var controlBuilder = WebApplication.CreateBuilder();
        controlBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        controlBuilder.Services.AddSingleton<IArtifactDeliveryApplicationService>(delivery);
        controlBuilder.Services.AddSingleton(authenticator);
        controlBuilder.Services.AddSingleton(Substitute.For<IControlPlaneConnectionTokenIssuer>());
        controlBuilder.Services.AddSingleton(Substitute.For<IControlPlaneConnectionTokenValidator>());
        await using var control = controlBuilder.Build();
        control.MapOrchestratorArtifactDeliveryEndpoints();
        await control.StartAsync();

        var source = new ControlPlaneDistributionSource { Key = "control", EndpointBaseUri = control.Urls.Single(), RemoteRuntimeNodeId = nodeId.ToString() };
        var sourceProvider = Substitute.For<IControlPlaneDistributionSourceProvider>();
        sourceProvider.GetByKey("control").Returns(source);
        sourceProvider.GetAll().Returns([source]);
        var runtimeBuilder = WebApplication.CreateBuilder();
        runtimeBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        runtimeBuilder.Services.AddSingleton(sourceProvider);
        runtimeBuilder.Services.AddKrackendOrchestrationsRuntime();
        runtimeBuilder.Services.AddSingleton(Substitute.For<IControlPlaneAccessTokenProvider>());
        var ingressStandup = Substitute.For<IRuntimeIngressStandupScheduler>();
        runtimeBuilder.Services.AddSingleton(ingressStandup);
        await using var runtime = runtimeBuilder.Build();
        runtime.MapOrchestratorRuntimeDistributionEndpoints();
        await runtime.StartAsync();

        using var http = new HttpClient { BaseAddress = new Uri(runtime.Urls.Single()) };
        var pending = await http.GetFromJsonAsync<RuntimeArtifactDeliveryPackage[]>("/runtime/distribution/control-planes/control/artifacts/pending");
        Assert.Equal(targetId.ToString(), Assert.Single(pending!).ReleaseTargetId);
        using var response = await http.PostAsync($"/runtime/distribution/control-planes/control/artifacts/{targetId}/apply", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RuntimeArtifactDeploymentResult>();
        Assert.True(result!.Accepted, result.Message);
        using var scope = runtime.Services.CreateScope();
        var installed = await scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>().GetById(artifactId);
        Assert.Equal("test.sales", installed.OrchestrationDefinitionKey);
        Assert.Equal(releaseId.ToString(), installed.LastReleaseId);
        Assert.Equal(artifactId.ToString(), installed.LastControlPlaneArtifactId);
        await ingressStandup.Received(1).ScheduleStandupAsync(Arg.Any<RuntimeIngressStandupRequest>(), Arg.Any<CancellationToken>());
        var acknowledged = await targets.GetById(targetId);
        Assert.Equal(ReleaseTargetStatus.Acknowledged, acknowledged.Status);
        Assert.Equal(artifactId.ToString(), acknowledged.RuntimeVersionApplied);
    }
}
