using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

public sealed class ControlPlaneRuntimeNodeTests
{
    [Fact]
    public async Task RuntimeNodeApplicationServiceCreatesUpdatesMapsAndDeletesNodes()
    {
        var repository = new RecordingRuntimeNodeRepository();
        var service = new RuntimeNodeApplicationService(repository);

        var id = await service.Upsert(new UpsertRuntimeNodeInput(
            "",
            " Local Runtime ",
            " local-runtime ",
            DistributionMode.DesignPublishesToRuntime,
            " https://runtime.local ",
            " dev node "));

        var created = await repository.GetById(new Id(Ulid.Parse(id)));
        Assert.Equal("Local Runtime", created.Name);
        Assert.Equal("local-runtime", created.Code);
        Assert.Equal(RuntimeNodeStatus.Pending, created.Status);
        Assert.Equal(86_400, created.AccessTokenTtlSeconds);
        Assert.Equal(300, created.TokenRefreshSkewSeconds);

        created.EndpointApiPath = "custom/deploy";
        created.OutboundClientId = "client";
        created.OutboundKeyId = "key";
        created.ProtectedOutboundSecret = "secret";
        created.OutboundCredentialStatus = ConnectionCredentialStatus.Active;
        created.InboundCredentialStatus = ConnectionCredentialStatus.Active;
        repository.Nodes[created.Id] = created;

        var updatedId = await service.Upsert(new UpsertRuntimeNodeInput(
            id,
            "Runtime updated",
            "runtime-updated",
            DistributionMode.HybridSync,
            "https://runtime-updated.local",
            "updated"));

        var updated = await repository.GetById(new Id(Ulid.Parse(updatedId)));
        Assert.Equal("custom/deploy", updated.EndpointApiPath);
        Assert.Equal("client", updated.OutboundClientId);
        Assert.Equal(ConnectionCredentialStatus.Active, updated.OutboundCredentialStatus);

        await service.SetStatus(id, RuntimeNodeStatus.Enabled);
        Assert.Equal(RuntimeNodeStatus.Enabled, updated.Status);
        Assert.True(updated.IsEnabled);

        var mapped = await service.GetAll(new ApplicationPagedSettings { PageNumber = 1, PageSize = 10 });
        Assert.Single(mapped.Rows);
        Assert.Equal("Enabled", mapped.Rows.Single().Status);

        await service.SetStatus(id, RuntimeNodeStatus.Suspend);
        await service.SetStatus(id, RuntimeNodeStatus.Pending);
        await service.Delete(id);

        Assert.True(updated.IsDeleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Upsert(new UpsertRuntimeNodeInput(id, "Deleted", "deleted", DistributionMode.HybridSync, "", "")));
    }

    [Fact]
    public async Task RuntimeNodeApplicationServiceBlocksInvalidTransitionsAndMissingCredentials()
    {
        var repository = new RecordingRuntimeNodeRepository();
        var service = new RuntimeNodeApplicationService(repository);
        var pullOnly = RuntimeNode(DistributionMode.RuntimeFetchesFromDesign);
        pullOnly.InboundCredentialStatus = ConnectionCredentialStatus.Missing;
        var push = RuntimeNode(DistributionMode.DesignPublishesToRuntime);
        push.OutboundCredentialStatus = ConnectionCredentialStatus.Missing;
        push.EndpointBaseUri = "";
        repository.Nodes[pullOnly.Id] = pullOnly;
        repository.Nodes[push.Id] = push;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetStatus(pullOnly.Id.ToString(), RuntimeNodeStatus.Enabled));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetStatus(push.Id.ToString(), RuntimeNodeStatus.Enabled));

        push.EndpointBaseUri = "https://runtime.local";
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetStatus(push.Id.ToString(), RuntimeNodeStatus.Enabled));

        push.OutboundCredentialStatus = ConnectionCredentialStatus.Active;
        await service.SetStatus(push.Id.ToString(), RuntimeNodeStatus.Enabled);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetStatus(push.Id.ToString(), RuntimeNodeStatus.Pending));
    }

    [Fact]
    public async Task RuntimeNodeRepositoryPersistsUpdatesFiltersAndSoftDeletesNodes()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRuntimeNodeRepository>();
        var node = RuntimeNode(DistributionMode.HybridSync);
        node.Name = "Zulu Runtime";
        node.Code = "zulu";
        node.InboundClientId = "inbound-client";

        await repository.Create(node);

        var byId = await repository.GetById(node.Id);
        var byCode = await repository.GetByCode("zulu");
        var byClient = await repository.GetByInboundClientId("inbound-client");
        Assert.Equal(node.Id, byId.Id);
        Assert.Equal(node.Id, byCode.Id);
        Assert.Equal(node.Id, byClient.Id);

        node.Name = "Alpha Runtime";
        node.Code = "alpha";
        node.EndpointBaseUri = "https://alpha.local";
        node.InboundLastFailureReason = "none";
        node.OutboundCredentialStatus = ConnectionCredentialStatus.Active;
        await repository.Update(node);
        await repository.SetStatus(node.Id, RuntimeNodeStatus.Enabled);

        var updated = await repository.GetById(node.Id);
        Assert.Equal("Alpha Runtime", updated.Name);
        Assert.Equal(RuntimeNodeStatus.Enabled, updated.Status);
        Assert.True(updated.IsEnabled);

        var second = RuntimeNode(DistributionMode.RuntimeFetchesFromDesign);
        second.Name = "Beta Runtime";
        second.Code = "beta";
        await repository.Create(second);

        var page = await repository.GetAll(new PagedSettings(1, 10, [], []));
        Assert.Equal(["Alpha Runtime", "Beta Runtime"], page.Rows.Select(x => x.Name));

        await repository.SoftDelete(node.Id);
        Assert.Null(await repository.GetByCode("alpha"));
        Assert.Null(await repository.GetByInboundClientId("inbound-client"));
        Assert.Single((await repository.GetAll(new PagedSettings(1, 10, [], []))).Rows);

        var deleted = await repository.GetById(node.Id);
        Assert.True(deleted.IsDeleted);
        Assert.Equal(RuntimeNodeStatus.Suspend, deleted.Status);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<Sieve.Models.SieveOptions>(_ => { });
        services.AddOrchestratorControlPlaneStorageEntityFramework(options =>
            options.UseInMemoryDatabase($"control-plane-runtime-nodes-{Guid.NewGuid():N}"));
        return services.BuildServiceProvider();
    }

    private static RuntimeNode RuntimeNode(DistributionMode mode)
        => new()
        {
            Id = Id.New(),
            Name = "Local Runtime",
            Code = $"runtime-{Guid.NewGuid():N}",
            DistributionMode = mode,
            EndpointBaseUri = "https://runtime.local",
            EndpointApiPath = "runtime/artifacts/deploy",
            Status = RuntimeNodeStatus.Pending,
            IsEnabled = false,
            IsDeleted = false,
            Description = "Runtime node",
            AccessTokenTtlSeconds = 86_400,
            TokenRefreshSkewSeconds = 300,
            TokenValidationCacheTtlSeconds = 300,
            InboundClientId = "inbound",
            InboundKeyId = "inbound-key",
            InboundSecretHash = "hash",
            InboundAllowedScopes = "ArtifactRead ArtifactAcknowledge ConnectionValidate",
            InboundCredentialStatus = ConnectionCredentialStatus.Active,
            InboundCredentialCreatedAtUtc = DateTime.UtcNow,
            InboundLastTokenIssuedAtUtc = DateTime.UtcNow,
            InboundLastFailureReason = "",
            OutboundClientId = "outbound",
            OutboundKeyId = "outbound-key",
            ProtectedOutboundSecret = "protected",
            OutboundRequestedScopes = "ArtifactPush ConnectionValidate",
            OutboundCredentialStatus = ConnectionCredentialStatus.Active,
            OutboundCredentialImportedAtUtc = DateTime.UtcNow,
            OutboundLastTokenReceivedAtUtc = DateTime.UtcNow,
            RegisteredAtUtc = DateTime.UtcNow,
            LastUpdatedAtUtc = DateTime.UtcNow
        };

    private sealed class RecordingRuntimeNodeRepository : IRuntimeNodeRepository
    {
        public Dictionary<Id, RuntimeNode> Nodes { get; } = new();

        public Task Create(RuntimeNode runtimeNode, CancellationToken cancellationToken = default)
        {
            Nodes[runtimeNode.Id] = runtimeNode;
            return Task.CompletedTask;
        }

        public Task Update(RuntimeNode runtimeNode, CancellationToken cancellationToken = default)
        {
            Nodes[runtimeNode.Id] = runtimeNode;
            return Task.CompletedTask;
        }

        public Task SetStatus(Id runtimeNodeId, RuntimeNodeStatus status, CancellationToken cancellationToken = default)
        {
            Nodes[runtimeNodeId].Status = status;
            Nodes[runtimeNodeId].IsEnabled = status == RuntimeNodeStatus.Enabled;
            return Task.CompletedTask;
        }

        public Task SoftDelete(Id runtimeNodeId, CancellationToken cancellationToken = default)
        {
            Nodes[runtimeNodeId].IsDeleted = true;
            Nodes[runtimeNodeId].DeletedAtUtc = DateTime.UtcNow;
            Nodes[runtimeNodeId].Status = RuntimeNodeStatus.Suspend;
            Nodes[runtimeNodeId].IsEnabled = false;
            return Task.CompletedTask;
        }

        public Task<RuntimeNode> GetById(Id runtimeNodeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Nodes[runtimeNodeId]);

        public Task<RuntimeNode> GetByCode(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(Nodes.Values.FirstOrDefault(x => !x.IsDeleted && x.Code == code)!);

        public Task<RuntimeNode> GetByInboundClientId(string clientId, CancellationToken cancellationToken = default)
            => Task.FromResult(Nodes.Values.FirstOrDefault(x => !x.IsDeleted && x.InboundClientId == clientId)!);

        public Task<PagedResult<RuntimeNode>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
        {
            var rows = Nodes.Values
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .ToArray();
            return Task.FromResult(new PagedResult<RuntimeNode>(pagedSettings.PageNumber, 1, rows.Length, pagedSettings.PageSize, rows));
        }
    }
}
