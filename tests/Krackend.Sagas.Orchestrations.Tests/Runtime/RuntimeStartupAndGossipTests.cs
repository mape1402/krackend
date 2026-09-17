namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

public sealed class RuntimeStartupAndGossipTests
{
    [Fact]
    public async Task StartupServiceSchedulesStandupForReadyArtifactsAndShutsDownRegistryOnStop()
    {
        var artifacts =
            new[]
            {
                Artifact("sales.sale.created", 3),
                Artifact("sales.sale.created", 4)
            };
        var artifactRepository = Substitute.For<IRuntimeArtifactRepository>();
        var standupScheduler = Substitute.For<IRuntimeIngressStandupScheduler>();
        var ingressRegistry = Substitute.For<IIngressRegistry>();
        artifactRepository.GetReady(Arg.Any<CancellationToken>()).Returns(artifacts);
        var services = new ServiceCollection();
        services.AddSingleton(artifactRepository);
        services.AddSingleton(standupScheduler);
        services.AddSingleton(ingressRegistry);
        await using var provider = services.BuildServiceProvider();
        var service = new RuntimeReadyArtifactStartupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TestRuntimeReplicaIdentity { ReplicaId = "replica-x" });

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        await standupScheduler.Received(1).ScheduleStandupAsync(
            Arg.Is<RuntimeIngressStandupRequest>(request =>
                request.ArtifactId == artifacts[0].Id.ToString() &&
                request.IngressGeneration == 3 &&
                request.Reason == "startup:replica-x"),
            Arg.Any<CancellationToken>());
        await standupScheduler.Received(1).ScheduleStandupAsync(
            Arg.Is<RuntimeIngressStandupRequest>(request =>
                request.ArtifactId == artifacts[1].Id.ToString() &&
                request.IngressGeneration == 4 &&
                request.Reason == "startup:replica-x"),
            Arg.Any<CancellationToken>());
        await ingressRegistry.Received(1).ShutDownAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GossipHandlerSchedulesLocalStandupFromReadyMessage()
    {
        var scheduler = Substitute.For<IRuntimeIngressStandupScheduler>();
        await using var provider = CreateGossipProvider(
            scheduler: scheduler,
            localHandler: null,
            publisher: Substitute.For<IRuntimeArtifactReadyGossipPublisher>(),
            gossipEnabled: false);
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactReadyGossipHandler>();
        var message = Message();

        await handler.HandleAsync(message);

        await scheduler.Received(1).ScheduleStandupAsync(
            Arg.Is<RuntimeIngressStandupRequest>(request =>
                request.ArtifactId == message.ArtifactId &&
                request.IngressGeneration == message.IngressGeneration &&
                request.Reason == "gossip"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadyNotifierAlwaysSchedulesLocalStandupAndPublishesOnlyWhenGossipIsEnabled()
    {
        var localHandler = Substitute.For<IRuntimeArtifactReadyGossipHandler>();
        var publisher = Substitute.For<IRuntimeArtifactReadyGossipPublisher>();
        var message = Message();

        await using (var disabledProvider = CreateGossipProvider(
            scheduler: Substitute.For<IRuntimeIngressStandupScheduler>(),
            localHandler: localHandler,
            publisher: publisher,
            gossipEnabled: false))
        {
            using var scope = disabledProvider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IRuntimeArtifactReadyNotifier>()
                .NotifyReadyAsync(message);
        }

        await using (var enabledProvider = CreateGossipProvider(
            scheduler: Substitute.For<IRuntimeIngressStandupScheduler>(),
            localHandler: localHandler,
            publisher: publisher,
            gossipEnabled: true))
        {
            using var scope = enabledProvider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IRuntimeArtifactReadyNotifier>()
                .NotifyReadyAsync(message);
        }

        await localHandler.Received(2).HandleAsync(message, Arg.Any<CancellationToken>());
        await publisher.Received(1).PublishAsync(message, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RuntimeArtifactReadyGossipMessageCopiesArtifactIdentity()
    {
        var artifact = Artifact("sales.sale.created", 7);

        var message = RuntimeArtifactReadyGossipMessage.FromArtifact(artifact);

        Assert.Equal(artifact.Id.ToString(), message.ArtifactId);
        Assert.Equal("sales.sale.created", message.OrchestrationDefinitionKey);
        Assert.Equal("1.0.0", message.Version);
        Assert.Equal(7, message.IngressGeneration);
        Assert.NotEqual(default, message.OccurredOnUtc);
    }

    private static ServiceProvider CreateGossipProvider(
        IRuntimeIngressStandupScheduler scheduler,
        IRuntimeArtifactReadyGossipHandler? localHandler,
        IRuntimeArtifactReadyGossipPublisher publisher,
        bool gossipEnabled)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddKrackendOrchestrationsRuntime();
        services.Configure<RuntimeGossipOptions>(options => options.Enabled = gossipEnabled);
        services.Replace(ServiceDescriptor.Singleton<IRuntimeIngressStandupScheduler>(scheduler));
        if (localHandler is not null)
        {
            services.Replace(ServiceDescriptor.Singleton(localHandler));
        }

        services.Replace(ServiceDescriptor.Singleton(publisher));
        return services.BuildServiceProvider();
    }

    private static RuntimeArtifactReadyGossipMessage Message()
        => new()
        {
            ArtifactId = Id.New().ToString(),
            OrchestrationDefinitionKey = "sales.sale.created",
            Version = "1.0.0",
            IngressGeneration = 5,
            OccurredOnUtc = DateTime.UtcNow
        };

    private static RuntimeOrchestrationArtifact Artifact(string key, long ingressGeneration)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = key,
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum($"checksum-{ingressGeneration}"),
            ArtifactPayload = JsonNode.Parse("""{"definitionKey":"sales.sale.created"}""")!,
            Status = RuntimeOrchestrationArtifactStatus.Ready,
            IngressGeneration = ingressGeneration,
            IsActive = true,
            DeployedOnUtc = DateTime.UtcNow
        };
}
