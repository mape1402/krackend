namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sieve.Services;

public sealed class PendingReleasePagingIntegrationTests
{
    [Fact]
    public async Task RuntimeClientReadsPagesFromAuthenticatedControlPlaneWithoutChangingTargets()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var sieve = Substitute.For<ISieveProcessor>();
        var environmentId = Id.New();
        context.DistributionEnvironments.Add(new DistributionEnvironmentEntity { Id = environmentId, Name = "Testing", Code = "test", Description = "", IsEnabled = true });
        await context.SaveChangesAsync();
        var nodes = new RuntimeNodeRepository(context, sieve);
        var nodeId = Id.New();
        await nodes.Create(new RuntimeNode
        {
            Id = nodeId, Name = "Runtime", Code = "runtime", EnvironmentId = environmentId,
            DistributionMode = DistributionMode.HybridSync, Status = RuntimeNodeStatus.Enabled,
            IsEnabled = true, Description = "", RegisteredAtUtc = DateTime.UtcNow
        });
        var artifacts = new ArtifactRepository(context, sieve);
        var artifactId = Id.New();
        await artifacts.Create(new Artifact
        {
            Id = artifactId, OrchestrationDefinitionId = "sales", OrchestrationVersionId = "v1",
            OrchestrationDisplayName = "Sales", VersionLabel = "Test", VersionNumber = "1.0.0",
            ArtifactType = "orchestration-version.new", SchemaVersion = "1", Payload = "{}", Metadata = "{}",
            SourceEvent = "test", SourceVersion = "1", Checksum = "checksum", CreatedAtUtc = DateTime.UtcNow
        });
        var targets = new ReleaseTargetRepository(context, sieve);
        for (var i = 0; i < 31; i++)
            await targets.Create(new ReleaseTarget
            {
                Id = Id.New(), RuntimeNodeId = nodeId, ArtifactId = artifactId,
                Status = ReleaseTargetStatus.Pending, ActivationStatus = ActivationStatus.NotActivated,
                AssignedAtUtc = new DateTime(2026, 1, 1).AddMinutes(i), CorrelationId = $"correlation-{i}"
            });

        var service = new ArtifactDeliveryApplicationService(new HttpClient(), targets, new ReleaseRepository(context, sieve),
            artifacts, nodes, Substitute.For<IRuntimeAccessTokenProvider>());
        var authenticator = Substitute.For<IArtifactDeliveryEndpointAuthenticator>();
        authenticator.AuthenticateRuntimeNodeAsync(Arg.Any<HttpRequest>(), nodeId.ToString(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ArtifactDeliveryEndpointAuthenticationResult.Success());
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IArtifactDeliveryApplicationService>(service);
        builder.Services.AddSingleton(authenticator);
        builder.Services.AddSingleton(Substitute.For<IControlPlaneConnectionTokenIssuer>());
        builder.Services.AddSingleton(Substitute.For<IControlPlaneConnectionTokenValidator>());
        await using var app = builder.Build();
        app.MapOrchestratorArtifactDeliveryEndpoints();
        await app.StartAsync();

        var source = new ControlPlaneDistributionSource { Key = "control", EndpointBaseUri = "http://localhost", RemoteRuntimeNodeId = nodeId.ToString() };
        var provider = Substitute.For<IControlPlaneDistributionSourceProvider>();
        provider.GetByKey("control").Returns(source);
        var client = new ControlPlaneArtifactPullService(app.GetTestClient(), provider,
            Substitute.For<IControlPlaneAccessTokenProvider>(), Substitute.For<IRuntimeArtifactDeploymentService>());
        var first = await client.GetPendingPageAsync("control", 1, 25);
        var second = await client.GetPendingPageAsync("control", 2, 25);
        Assert.Equal(31, first.TotalRows);
        Assert.Equal(25, first.Items.Count);
        Assert.Equal(6, second.Items.Count);
        Assert.Equal("correlation-0", first.Items.First().CorrelationId);
        Assert.Equal("correlation-30", second.Items.Last().CorrelationId);
        Assert.All(await context.ReleaseTargets.ToArrayAsync(), x => Assert.Equal(ReleaseTargetStatus.Pending, x.Status));
        await authenticator.Received(2).AuthenticateRuntimeNodeAsync(Arg.Any<HttpRequest>(), nodeId.ToString(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
