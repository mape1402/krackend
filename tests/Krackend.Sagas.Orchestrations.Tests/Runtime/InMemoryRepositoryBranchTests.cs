namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.DependencyInjection;

public sealed class InMemoryRepositoryBranchTests
{
    [Fact]
    public async Task TaskExecutionRepositoryCoversLookupSuccessFailureAndWaitingFilters()
    {
        await using var provider = CreateRuntimeProvider();
        var repository = provider.GetRequiredService<ITaskExecutionRepository>();
        var instanceId = Id.New();
        var stageId = Id.New();
        var due = DateTime.UtcNow.AddMinutes(-5);
        var waiting = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageExecutionId = stageId,
            TaskKey = "inventories.reserve",
            CorrelationId = "corr-1",
            Status = TaskExecutionStatus.WaitingResponse,
            WaitingSinceUtc = due
        };
        var running = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageExecutionId = stageId,
            TaskKey = "inventories.audit",
            CorrelationId = "corr-2",
            Status = TaskExecutionStatus.Running,
            WaitingSinceUtc = due
        };

        await repository.Create(waiting);
        await repository.Create(running);

        Assert.Same(waiting, await repository.GetByCorrelationId("corr-1"));
        Assert.Same(waiting, await repository.GetByStageAndKey(stageId, "inventories.reserve"));
        Assert.Single(await repository.GetWaitingResponseOlderThan(DateTime.UtcNow));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetByCorrelationId("missing"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetByStageAndKey(stageId, "missing"));
    }

    [Fact]
    public async Task OrchestrationInstanceRepositoryCoversIdempotencyLeaseAndSummaryBranches()
    {
        await using var provider = CreateRuntimeProvider();
        var repository = provider.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddMinutes(-10));
        instance.StartIdempotencyKey = "start-key";
        var failed = Instance(Id.New(), OrchestrationInstanceStatus.DeadLettered, now.AddMinutes(-20));
        failed.FailedOnUtc = now.AddMinutes(-1);

        await repository.Create(instance);
        await repository.Create(failed);

        Assert.Null(await repository.TryGetByStartIdempotencyKey(" "));
        Assert.Same(instance, await repository.TryGetByStartIdempotencyKey(" start-key "));
        var firstLease = await repository.TryAcquireLease(instance.Id, "lease-1", now, now.AddMinutes(5));
        var blockedLease = await repository.TryAcquireLease(instance.Id, "lease-2", now.AddMinutes(1), now.AddMinutes(6));
        await repository.ReleaseLease(instance.Id, "other-lease");
        await repository.ReleaseLease(Id.New(), "lease-1");
        Assert.Equal("lease-1", instance.ActiveLeaseId);
        await repository.ReleaseLease(instance.Id, "lease-1");
        var secondLease = await repository.TryAcquireLease(instance.Id, "lease-2", now.AddMinutes(6), now.AddMinutes(7));
        failed.ActiveLeaseId = "expired-lease";
        failed.ActiveLeaseExpiresOnUtc = now.AddMinutes(-1);
        var expiredLease = await repository.TryAcquireLease(failed.Id, "lease-3", now, now.AddMinutes(5));
        failed.ActiveLeaseId = "lease-without-expiration";
        failed.ActiveLeaseExpiresOnUtc = null;
        var missingExpirationLease = await repository.TryAcquireLease(failed.Id, "lease-4", now, now.AddMinutes(5));
        var summary = await repository.GetSummary(now.AddHours(-1));

        Assert.NotNull(firstLease);
        Assert.Null(blockedLease);
        Assert.NotNull(secondLease);
        Assert.NotNull(expiredLease);
        Assert.NotNull(missingExpirationLease);
        Assert.Equal("lease-2", instance.ActiveLeaseId);
        Assert.Equal("lease-4", failed.ActiveLeaseId);
        Assert.True(summary.Active >= 1);
        Assert.True(summary.FailedRecent >= 1);
    }

    [Fact]
    public async Task RuntimeIngressConfigurationRepositoryOnlyReturnsActiveReadyArtifacts()
    {
        await using var provider = CreateRuntimeProvider();
        var artifactRepository = provider.GetRequiredService<IRuntimeArtifactRepository>();
        var repository = provider.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var readyArtifact = Id.New();
        var inactiveArtifact = Id.New();
        var pendingArtifact = Id.New();
        await artifactRepository.Upsert(Artifact(readyArtifact, RuntimeOrchestrationArtifactStatus.Ready, isActive: true));
        await artifactRepository.Upsert(Artifact(inactiveArtifact, RuntimeOrchestrationArtifactStatus.Ready, isActive: false));
        await artifactRepository.Upsert(Artifact(pendingArtifact, RuntimeOrchestrationArtifactStatus.Pending, isActive: true));
        var readyConfiguration = Configuration(readyArtifact, "ready");
        var inactiveConfiguration = Configuration(inactiveArtifact, "inactive");
        var pendingConfiguration = Configuration(pendingArtifact, "pending");
        var missingConfiguration = Configuration(Id.New(), "missing");

        await repository.UpsertForArtifactAsync(readyArtifact, [readyConfiguration]);
        await repository.UpsertForArtifactAsync(inactiveArtifact, [inactiveConfiguration]);
        await repository.UpsertForArtifactAsync(pendingArtifact, [pendingConfiguration]);
        await repository.UpsertForArtifactAsync(missingConfiguration.RuntimeOrchestrationArtifactId, [missingConfiguration]);
        var active = await repository.ReadActiveAsync(0, 10);
        var readyByArtifact = await repository.GetActiveByArtifactIdAsync(readyArtifact);
        var inactiveByArtifact = await repository.GetActiveByArtifactIdAsync(inactiveArtifact);
        await repository.DeactivateForArtifactsAsync(null!);
        await repository.DeactivateForArtifactsAsync([]);
        await repository.DeactivateForArtifactsAsync([readyArtifact]);
        var afterDeactivate = await repository.GetActiveByArtifactIdAsync(readyArtifact);
        var replacement = Configuration(readyArtifact, "ready");
        replacement.SettingsPayload = """{"updated":true}""";
        await repository.UpsertForArtifactAsync(readyArtifact, [replacement]);
        var afterUpsert = await repository.GetActiveByArtifactIdAsync(readyArtifact);

        Assert.Equal("ready", Assert.Single(active).ConfigurationKey);
        Assert.Single(readyByArtifact);
        Assert.Empty(inactiveByArtifact);
        Assert.Empty(afterDeactivate);
        Assert.Equal("""{"updated":true}""", Assert.Single(afterUpsert).SettingsPayload);
    }

    private static ServiceProvider CreateRuntimeProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        return services.BuildServiceProvider();
    }

    private static OrchestrationInstance Instance(
        Id id,
        OrchestrationInstanceStatus status,
        DateTime startedOnUtc)
        => new()
        {
            Id = id,
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = $"corr-{id}",
            SagaId = $"saga-{id}",
            ExecutionKey = $"sales.sale.created:{id}",
            Status = status,
            StartedOnUtc = startedOnUtc,
            LastUpdatedOnUtc = startedOnUtc,
            SnapshotPayload = JsonNode.Parse("{}")
        };

    private static RuntimeOrchestrationArtifact Artifact(
        Id id,
        RuntimeOrchestrationArtifactStatus status,
        bool isActive)
        => new()
        {
            Id = id,
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum("checksum"),
            ArtifactPayload = JsonNode.Parse("{}")!,
            Status = status,
            IsActive = isActive,
            DeployedOnUtc = DateTime.UtcNow
        };

    private static RuntimeIngressConfiguration Configuration(Id artifactId, string key)
        => new()
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = artifactId,
            ConfigurationKey = key,
            IngressKind = IngressKind.Trigger,
            IngressTransport = IngressTransport.Messaging,
            SettingsPayload = "{}",
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };
}
