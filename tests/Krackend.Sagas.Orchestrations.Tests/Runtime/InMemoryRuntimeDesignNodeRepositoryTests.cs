using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class InMemoryRuntimeDesignNodeRepositoryTests
{
    [Fact]
    public async Task RuntimeDesignNodeRepositoryStoresQueriesAndUpdatesNodesInMemory()
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IRuntimeDesignNodeRepository>();
        var beta = Node(
            key: "beta",
            name: "Beta",
            status: RuntimeDesignNodeStatus.Enabled,
            isEnabled: true,
            inboundClientId: "in-beta");
        var alphaB = Node(
            key: "alpha-b",
            name: "Alpha",
            status: RuntimeDesignNodeStatus.Enabled,
            isEnabled: false,
            inboundClientId: "in-alpha-b");
        var alphaA = Node(
            key: "alpha-a",
            name: "Alpha",
            status: RuntimeDesignNodeStatus.Pending,
            isEnabled: false,
            inboundClientId: "in-alpha-a");

        await repository.UpsertAsync(beta);
        await repository.UpsertAsync(alphaB);
        await repository.UpsertAsync(alphaA);
        await repository.UpsertAsync(Node(
            key: "beta-updated",
            name: "Beta Updated",
            status: RuntimeDesignNodeStatus.Enabled,
            isEnabled: true,
            inboundClientId: "in-beta-updated",
            id: beta.Id));

        var all = await repository.GetAllAsync();

        Assert.Equal(["alpha-a", "alpha-b", "beta-updated"], all.Select(x => x.Key).ToArray());
        Assert.Equal(beta.Id, (await repository.GetByKeyAsync("BETA-UPDATED"))!.Id);
        Assert.Equal(beta.Id, (await repository.GetByInboundClientIdAsync("in-beta-updated"))!.Id);
        Assert.Null(await repository.GetByIdAsync(Id.New()));
        Assert.Null(await repository.GetByKeyAsync("missing"));
        Assert.Null(await repository.GetByInboundClientIdAsync("IN-BETA-UPDATED"));
        Assert.Equal([beta.Id], repository.GetEnabled().Select(x => x.Id).ToArray());

        await repository.SetStatusAsync(alphaA.Id, RuntimeDesignNodeStatus.Enabled);
        await repository.SetEnabledAsync(alphaB.Id, true);
        await repository.SetEnabledAsync(beta.Id, false);
        await repository.SetStatusAsync(Id.New(), RuntimeDesignNodeStatus.Enabled);

        Assert.Equal([alphaA.Id, alphaB.Id], repository.GetEnabled().Select(node => node.Id).ToArray());
        Assert.DoesNotContain(repository.GetEnabled(), node => node.Id == beta.Id);
        Assert.Equal(RuntimeDesignNodeStatus.Suspend, (await repository.GetByIdAsync(beta.Id))!.Status);
    }

    [Fact]
    public async Task DistributionSourceProviderFiltersEnabledPullCapableNodesAndFindsSources()
    {
        using var provider = CreateProvider();
        var repository = provider.GetRequiredService<IRuntimeDesignNodeRepository>();
        var sourceProvider = provider.GetRequiredService<IControlPlaneDistributionSourceProvider>();
        var pull = Node(
            key: "pull",
            name: "Pull",
            status: RuntimeDesignNodeStatus.Enabled,
            isEnabled: true,
            inboundClientId: "in-pull");
        pull.DistributionMode = DistributionConnectionMode.RuntimeFetchesFromDesign;
        pull.OutboundClientId = "out-pull";
        var pushOnly = Node(
            key: "push",
            name: "Push",
            status: RuntimeDesignNodeStatus.Enabled,
            isEnabled: true,
            inboundClientId: "in-push");
        pushOnly.DistributionMode = DistributionConnectionMode.DesignPublishesToRuntime;
        pushOnly.OutboundClientId = "out-push";

        await repository.UpsertAsync(pull);
        await repository.UpsertAsync(pushOnly);

        var all = sourceProvider.GetAll();

        Assert.Single(all);
        Assert.Equal("pull", sourceProvider.GetByKey("PULL").Key);
        Assert.Equal("pull", sourceProvider.GetByClientId("out-pull").Key);
        Assert.Throws<KeyNotFoundException>(() => sourceProvider.GetByKey("missing"));
        Assert.Throws<KeyNotFoundException>(() => sourceProvider.GetByClientId("out-push"));
    }

    [Fact]
    public async Task DefaultGossipPublisherIgnoresArtifactReadyMessagesWhenAdapterIsNotConfigured()
    {
        using var provider = CreateProvider();
        var publisher = provider.GetRequiredService<IRuntimeArtifactReadyGossipPublisher>();

        await publisher.PublishAsync(new RuntimeArtifactReadyGossipMessage
        {
            ArtifactId = Id.New().ToString(),
            OrchestrationDefinitionKey = "sales.sale.created",
            Version = "1.0.0",
            IngressGeneration = 1,
            OccurredOnUtc = DateTime.UtcNow
        });
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsRuntime();
        return services.BuildServiceProvider();
    }

    private static RuntimeDesignNode Node(
        string key,
        string name,
        RuntimeDesignNodeStatus status,
        bool isEnabled,
        string inboundClientId,
        Id? id = null)
        => new()
        {
            Id = id ?? Id.New(),
            Key = key,
            Name = name,
            EndpointBaseUri = $"https://{key}.local",
            RemoteRuntimeNodeId = $"remote-{key}",
            DistributionMode = DistributionConnectionMode.HybridSync,
            AccessTokenTtlSeconds = 86400,
            TokenRefreshSkewSeconds = 300,
            TokenValidationCacheTtlSeconds = 300,
            InboundClientId = inboundClientId,
            InboundKeyId = $"in-key-{key}",
            InboundSecretHash = "hash",
            InboundAllowedScopes = "artifact:push",
            InboundCredentialStatus = ConnectionCredentialStatus.Active,
            InboundCredentialCreatedAtUtc = DateTime.UtcNow,
            OutboundClientId = $"out-client-{key}",
            OutboundKeyId = $"out-key-{key}",
            ProtectedOutboundSecret = "protected",
            OutboundRequestedScopes = "release:read artifact:read artifact:ack",
            OutboundCredentialStatus = ConnectionCredentialStatus.Active,
            OutboundCredentialImportedAtUtc = DateTime.UtcNow,
            Description = "test node",
            Status = status,
            IsEnabled = isEnabled,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };
}
