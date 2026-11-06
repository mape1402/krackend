namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json.Nodes;

public sealed class IngressRegistryLifecycleTests
{
    [Fact]
    public async Task ArchivedCleanupRechecksStateAndRetriesFailedDisconnect()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        var artifactId = Id.New();
        var artifact = new RuntimeOrchestrationArtifact
        {
            Id = artifactId, OrchestrationDefinitionKey = "orders", ArtifactType = "orchestration-version-snapshot",
            Version = new SemanticVersion(1, 0, 0), ArtifactChecksum = new Checksum("checksum"), ArtifactPayload = new JsonObject(),
            IsActive = true, Status = RuntimeOrchestrationArtifactStatus.Ready, LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived
        };
        var artifacts = Substitute.For<IRuntimeArtifactRepository>();
        artifacts.GetById(artifactId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(artifact));
        services.Replace(ServiceDescriptor.Scoped(_ => artifacts));
        var accessor = Substitute.For<IGetIngressConfigurationByArtifactAccessor>();
        accessor.GetConfigurationAsync(artifactId.ToString(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>([new IngressConfiguration
            {
                Id = "backchannel", ArtifactId = artifactId.ToString(), IngressKind = IngressKind.Backchannel,
                IngressTransport = IngressTransport.Messaging, SettingsPayload = "{}"
            }]));
        services.Replace(ServiceDescriptor.Singleton(accessor));
        var connector = Substitute.For<IIngressConector>();
        connector.DisconnectAsync("backchannel", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Disconnect failed")), Task.CompletedTask);
        services.RemoveAllKeyed<IIngressConector>(IngressTransport.Messaging);
        services.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, (_, _) => connector);
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IIngressRegistry>();
        await registry.StandUpOneAsync(artifactId.ToString(), 1);
        var cleanup = Assert.IsAssignableFrom<IArchivedRecoveryIngressCleanup>(registry);

        Assert.False(await cleanup.ShutDownIfArchivedAsync(artifactId.ToString()));
        await connector.DidNotReceive().DisconnectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        artifact.IsActive = false;
        artifact.Status = RuntimeOrchestrationArtifactStatus.Retired;
        await Assert.ThrowsAsync<InvalidOperationException>(() => cleanup.ShutDownIfArchivedAsync(artifactId.ToString()));
        Assert.Contains(artifactId.ToString(), cleanup.GetLocalArtifactIds());
        Assert.True(await cleanup.ShutDownIfArchivedAsync(artifactId.ToString()));
        Assert.DoesNotContain(artifactId.ToString(), cleanup.GetLocalArtifactIds());
    }

    [Fact]
    public async Task ArchivedRecoveryPreparesOnlyBackchannelAndCleansItIfActivationFails()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        var backchannel = CreateConfigurations()[1];
        var accessor = Substitute.For<IGetArchivedBackchannelConfigurationAccessor>();
        accessor.GetArchivedBackchannelsAsync("artifact-1", 7, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>([backchannel]));
        services.AddSingleton(accessor);
        var connector = Substitute.For<IIngressConector>();
        services.RemoveAllKeyed<IIngressConector>(IngressTransport.Messaging);
        services.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, (_, _) => connector);
        await using var provider = services.BuildServiceProvider();
        var registry = Assert.IsAssignableFrom<IArchivedRecoveryIngressRegistry>(provider.GetRequiredService<IIngressRegistry>());

        await using (await registry.PrepareArchivedRecoveryAsync("artifact-1", 7)) { }

        await connector.Received(1).ConnectAsync(Arg.Is<IngressConfiguration>(x => x.Id == "backchannel-ingress" && x.IsStaged), Arg.Any<CancellationToken>());
        await connector.Received(1).DisconnectAsync("backchannel-ingress", Arg.Any<CancellationToken>());
        await connector.DidNotReceive().ConnectAsync(Arg.Is<IngressConfiguration>(x => x.IngressKind == IngressKind.Trigger), Arg.Any<CancellationToken>());

        accessor.GetArchivedBackchannelsAsync("artifact-1", 8, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>(CreateConfigurations()));
        await Assert.ThrowsAsync<IngressStandupConfigurationException>(() => registry.PrepareArchivedRecoveryAsync("artifact-1", 8));
    }

    [Fact]
    public async Task FailedRollbackPreparationCleansOnlyItsNewConnectors()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        var configurations = CreateConfigurations();
        var ready = Substitute.For<IGetIngressConfigurationByArtifactAccessor>();
        ready.GetConfigurationAsync("artifact-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>([configurations[1]]));
        var rollback = Substitute.For<IGetRollbackIngressConfigurationAccessor>();
        rollback.GetRollbackConfigurationAsync("artifact-1", 8, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>(configurations));
        services.Replace(ServiceDescriptor.Singleton(ready));
        services.AddSingleton(rollback);
        var connector = Substitute.For<IIngressConector, IRevalidatableIngressConector>();
        services.RemoveAllKeyed<IIngressConector>(IngressTransport.Messaging);
        services.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, (_, _) => connector);
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IIngressRegistry>();
        await registry.StandUpOneAsync("artifact-1", 7);

        var preparation = await Assert.IsAssignableFrom<IRollbackIngressRegistry>(registry)
            .PrepareRollbackAsync("artifact-1", 8, ["trigger-key"]);
        await preparation.DisposeAsync();

        await connector.Received(1).DisconnectAsync("trigger-ingress", Arg.Any<CancellationToken>());
        await connector.DidNotReceive().DisconnectAsync("backchannel-ingress", Arg.Any<CancellationToken>());
        await ((IRevalidatableIngressConector)connector).Received(1)
            .RevalidateAsync(Arg.Is<IngressConfiguration>(x => x.Id == "backchannel-ingress" && x.IsStaged), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshReadsNewConnectorsEvenWhenGenerationWasAlreadyApplied()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        var reader = Substitute.For<IGetIngressConfigurationByArtifactAccessor>();
        var configurations = CreateConfigurations();
        reader.GetConfigurationAsync("artifact-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>([configurations[1]]),
                Task.FromResult<IReadOnlyCollection<IngressConfiguration>>(configurations));
        services.Replace(ServiceDescriptor.Singleton(reader));
        var connector = new RecordingIngressConnector();
        services.RemoveAllKeyed<IIngressConector>(IngressTransport.Messaging);
        services.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, (_, _) => connector);
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IIngressRegistry>();

        await registry.StandUpOneAsync("artifact-1", 7);
        await Assert.IsAssignableFrom<IRefreshIngressRegistry>(registry).RefreshOneAsync("artifact-1", 7);

        Assert.Equal(["backchannel-ingress", "trigger-ingress"], connector.ConnectedConnectorIds);
    }

    [Fact]
    public async Task RefreshDisconnectsRemovedTriggersButKeepsBackchannel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        var reader = Substitute.For<IGetIngressConfigurationByArtifactAccessor>();
        var configurations = CreateConfigurations();
        reader.GetConfigurationAsync("artifact-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>(configurations),
                Task.FromResult<IReadOnlyCollection<IngressConfiguration>>([configurations[1]]));
        services.Replace(ServiceDescriptor.Singleton(reader));
        var connector = new RecordingIngressConnector();
        services.RemoveAllKeyed<IIngressConector>(IngressTransport.Messaging);
        services.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, (_, _) => connector);
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IIngressRegistry>();

        await registry.StandUpOneAsync("artifact-1", 7);
        await Assert.IsAssignableFrom<IRefreshIngressRegistry>(registry).RefreshOneAsync("artifact-1", 7);

        Assert.Equal(["trigger-ingress"], connector.DisconnectedConnectorIds);
    }

    [Fact]
    public async Task StagedStandupUsesPendingAccessorAndMarksConnectorConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        var stagedAccessor = Substitute.For<IGetStagedIngressConfigurationAccessor>();
        stagedAccessor.GetStagedConfigurationAsync("artifact-1", 7, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>(CreateConfigurations()));
        var connector = Substitute.For<IIngressConector>();
        services.AddSingleton(stagedAccessor);
        services.RemoveAllKeyed<IIngressConector>(IngressTransport.Messaging);
        services.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, (_, _) => connector);
        await using var provider = services.BuildServiceProvider();
        var registry = Assert.IsAssignableFrom<IStagedIngressRegistry>(provider.GetRequiredService<IIngressRegistry>());

        await registry.StandUpStagedAsync("artifact-1", 7);

        await connector.Received(2).ConnectAsync(Arg.Is<IngressConfiguration>(x => x.IsStaged), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShutDownOneDisconnectsConnectorsAndAllowsSameGenerationStandupAgain()
    {
        var connector = new RecordingIngressConnector();
        var registry = CreateRegistry(connector);

        await registry.StandUpOneAsync("artifact-1", 1);
        await registry.ShutDownOneAsync("artifact-1");
        await registry.StandUpOneAsync("artifact-1", 1);

        Assert.Equal(["trigger-ingress", "backchannel-ingress", "trigger-ingress", "backchannel-ingress"], connector.ConnectedConnectorIds);
        Assert.Equal(2, connector.DisconnectedConnectorIds.Count);
        Assert.Contains("trigger-ingress", connector.DisconnectedConnectorIds);
        Assert.Contains("backchannel-ingress", connector.DisconnectedConnectorIds);
    }

    private static IIngressRegistry CreateRegistry(RecordingIngressConnector connector)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddKrackendOrchestrationsRuntime();
        services.RemoveAll<IGetIngressConfigurationByArtifactAccessor>();
        services.RemoveAllKeyed<IIngressConector>(IngressTransport.Messaging);
        services.AddSingleton<IGetIngressConfigurationByArtifactAccessor>(
            new StaticIngressConfigurationByArtifactAccessor(CreateConfigurations()));
        services.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, (_, _) => connector);

        return services.BuildServiceProvider().GetRequiredService<IIngressRegistry>();
    }

    private static IngressConfiguration[] CreateConfigurations()
        =>
        [
            new()
            {
                Id = "trigger-ingress",
                ArtifactId = "artifact-1",
                IngressKind = IngressKind.Trigger,
                IngressTransport = IngressTransport.Messaging,
                SettingsPayload = "{}"
            },
            new()
            {
                Id = "backchannel-ingress",
                ArtifactId = "artifact-1",
                IngressKind = IngressKind.Backchannel,
                IngressTransport = IngressTransport.Messaging,
                SettingsPayload = "{}"
            }
        ];
}
