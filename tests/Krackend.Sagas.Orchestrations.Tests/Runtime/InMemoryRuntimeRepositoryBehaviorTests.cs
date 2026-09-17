namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.DependencyInjection;

public sealed class InMemoryRuntimeRepositoryBehaviorTests
{
    [Fact]
    public async Task OrchestrationInstanceRepositoryAcquiresExpiresAndReleasesLeases()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = Instance(OrchestrationInstanceStatus.Running, DateTime.UtcNow.AddMinutes(-5));
        await repository.Create(instance);

        var firstLease = await repository.TryAcquireLease(
            instance.Id,
            "lease-1",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1));
        var blockedLease = await repository.TryAcquireLease(
            instance.Id,
            "lease-2",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1));
        var expiredLease = await repository.TryAcquireLease(
            instance.Id,
            "lease-3",
            DateTime.UtcNow.AddMinutes(2),
            DateTime.UtcNow.AddMinutes(3));

        await repository.ReleaseLease(instance.Id, "lease-2");
        Assert.Equal("lease-3", instance.ActiveLeaseId);

        await repository.ReleaseLease(instance.Id, "lease-3");

        Assert.NotNull(firstLease);
        Assert.Null(blockedLease);
        Assert.NotNull(expiredLease);
        Assert.Null(instance.ActiveLeaseId);
        Assert.Null(instance.ActiveLeaseExpiresOnUtc);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetById(Id.New()));
    }

    [Fact]
    public async Task OrchestrationInstanceRepositoryReturnsRecentInstancesAndStatusSummary()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var since = DateTime.UtcNow.AddMinutes(-10);
        var created = Instance(OrchestrationInstanceStatus.Created, since.AddMinutes(1));
        var running = Instance(OrchestrationInstanceStatus.Running, since.AddMinutes(2));
        var waiting = Instance(OrchestrationInstanceStatus.Waiting, since.AddMinutes(3));
        var completed = Instance(OrchestrationInstanceStatus.Completed, since.AddMinutes(4), completedOnUtc: since.AddMinutes(5));
        var failed = Instance(OrchestrationInstanceStatus.Failed, since.AddMinutes(6), failedOnUtc: since.AddMinutes(7));
        await repository.Create(created);
        await repository.Create(running);
        await repository.Create(waiting);
        await repository.Create(completed);
        await repository.Create(failed);

        var recent = await repository.GetRecent(take: 2);
        var summary = await repository.GetSummary(since);

        Assert.Equal([failed.Id, completed.Id], recent.Select(x => x.Id).ToArray());
        Assert.Equal(2, summary.Active);
        Assert.Equal(1, summary.Waiting);
        Assert.Equal(1, summary.CompletedRecent);
        Assert.Equal(1, summary.FailedRecent);
    }

    [Fact]
    public async Task TaskDispatchRepositoryFindsMarksAndFiltersDispatches()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITaskDispatchRepository>();
        var due = Dispatch("cmd-due", DateTime.UtcNow.AddMinutes(-2));
        var future = Dispatch("cmd-future", DateTime.UtcNow.AddMinutes(10));
        await repository.Create(due);
        await repository.Create(future);

        await repository.MarkSent(due.Id, "Sent", DateTime.UtcNow, "external-1");
        await repository.MarkFailed(future.Id, "broker down", "external-2");
        var byId = await repository.GetById(due.Id);
        var byCommand = await repository.GetByCommandId("cmd-due");
        var byAttempt = await repository.GetByAttemptId(due.TaskExecutionAttemptId);
        var missing = await repository.TryGetById(Id.New());
        var scheduled = await repository.GetScheduledOlderThan(DateTime.UtcNow);

        Assert.Equal("Sent", byId.DispatchStatus);
        Assert.NotNull(byId.SentOnUtc);
        Assert.Equal(due.Id, byCommand.Id);
        Assert.Equal(due.Id, byAttempt.Id);
        Assert.Null(missing);
        Assert.Equal("Failed", future.DispatchStatus);
        Assert.Equal("broker down", future.FailureReason);
        Assert.Single(scheduled);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetByCommandId("missing-command"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetByAttemptId(Id.New()));
    }

    [Fact]
    public async Task InMemoryRepositoriesPersistExecutionGraphArtifactsIngressAndUnitOfWork()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var stageRepository = services.GetRequiredService<IStageExecutionRepository>();
        var taskRepository = services.GetRequiredService<ITaskExecutionRepository>();
        var attemptRepository = services.GetRequiredService<ITaskExecutionAttemptRepository>();
        var dispatchRepository = services.GetRequiredService<ITaskDispatchRepository>();
        var compensationRepository = services.GetRequiredService<ICompensationExecutionRepository>();
        var artifactRepository = services.GetRequiredService<IRuntimeArtifactRepository>();
        var ingressRepository = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var unitOfWork = services.GetRequiredService<IRuntimeStorageUnitOfWork>();
        var instanceId = Id.New();
        var now = DateTime.UtcNow;

        await using (var transaction = await unitOfWork.BeginTransactionAsync())
        {
            await transaction.CommitAsync();
        }
        var defer = unitOfWork.DeferAutoSave();
        defer.Dispose();

        var stage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageKey = "inventory",
            Order = 2,
            Status = StageExecutionStatus.Running,
            StartedOnUtc = now
        };
        var firstStage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageKey = "created",
            Order = 1,
            Status = StageExecutionStatus.Completed
        };
        await stageRepository.Create(stage);
        await stageRepository.Create(firstStage);
        stage.Status = StageExecutionStatus.Completed;
        await stageRepository.Update(stage);

        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageExecutionId = stage.Id,
            TaskKey = "inventories.reserve",
            TaskKind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Status = TaskExecutionStatus.WaitingResponse,
            AwaitResponse = true,
            WaitingSinceUtc = now.AddMinutes(-5),
            CorrelationId = "task-correlation"
        };
        await taskRepository.Create(task);
        task.Status = TaskExecutionStatus.Completed;
        await taskRepository.Update(task);

        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 2,
            Status = TaskExecutionStatus.WaitingResponse,
            WaitingSinceUtc = now.AddMinutes(-4)
        };
        await attemptRepository.Create(attempt);
        attempt.Status = TaskExecutionStatus.Completed;
        await attemptRepository.Update(attempt);

        var dispatch = Dispatch("cmd-graph", now.AddMinutes(-3));
        dispatch.TaskExecutionAttemptId = attempt.Id;
        await dispatchRepository.Create(dispatch);

        var compensation = new CompensationExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            SourceTaskExecutionId = task.Id,
            CompensationTaskKey = "inventories.release",
            Status = "Pending",
            StartedOnUtc = now
        };
        await compensationRepository.Create(compensation);
        compensation.Status = "Completed";
        await compensationRepository.Update(compensation);

        var artifact = new RuntimeOrchestrationArtifact
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(1, 2, 0),
            ArtifactChecksum = new Checksum("checksum-memory"),
            ArtifactPayload = JsonNode.Parse("""{"definitionKey":"sales.sale.created"}""")!,
            Status = RuntimeOrchestrationArtifactStatus.Pending,
            IngressGeneration = 4,
            IsActive = true,
            DeployedOnUtc = now
        };
        await artifactRepository.Upsert(artifact);
        await artifactRepository.MarkReady(artifact.Id, 99);
        await artifactRepository.MarkProjectionFailed(artifact.Id, 99, "stale");
        await artifactRepository.MarkProjectionStarted(artifact.Id, artifact.IngressGeneration);
        await artifactRepository.MarkProjectionFailed(artifact.Id, artifact.IngressGeneration, "projection failed");
        await artifactRepository.MarkReady(artifact.Id, artifact.IngressGeneration);

        var ingress = new RuntimeIngressConfiguration
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = artifact.Id,
            ConfigurationKey = "trigger",
            IngressKind = IngressKind.Trigger,
            IngressTransport = IngressTransport.Messaging,
            SettingsPayload = """{"topic":"events.sales.sale.created"}""",
            IsActive = true,
            CreatedOnUtc = now,
            UpdatedOnUtc = now
        };
        await ingressRepository.UpsertForArtifactAsync(artifact.Id, [ingress]);
        await ingressRepository.UpsertForArtifactAsync(artifact.Id, [
            new RuntimeIngressConfiguration
            {
                Id = Id.New(),
                RuntimeOrchestrationArtifactId = artifact.Id,
                ConfigurationKey = "backchannel",
                IngressKind = IngressKind.Backchannel,
                IngressTransport = IngressTransport.Messaging,
                SettingsPayload = """{"topic":"orchestrations.sales.sale.created"}""",
                IsActive = true,
                CreatedOnUtc = now,
                UpdatedOnUtc = now
            },
            new RuntimeIngressConfiguration
            {
                Id = Id.New(),
                RuntimeOrchestrationArtifactId = artifact.Id,
                ConfigurationKey = "trigger",
                IngressKind = IngressKind.Trigger,
                IngressTransport = IngressTransport.Messaging,
                SettingsPayload = """{"topic":"updated"}""",
                IsActive = true,
                CreatedOnUtc = now,
                UpdatedOnUtc = now
            }
        ]);

        Assert.Equal(stage.Id, (await stageRepository.GetById(stage.Id)).Id);
        Assert.Equal(stage.Id, (await stageRepository.GetByInstanceAndKey(instanceId, "inventory")).Id);
        Assert.Equal([firstStage.Id, stage.Id], (await stageRepository.GetByInstanceId(instanceId)).Select(x => x.Id).ToArray());
        Assert.Equal(task.Id, (await taskRepository.GetById(task.Id)).Id);
        Assert.Equal(task.Id, (await taskRepository.GetByCorrelationId("task-correlation")).Id);
        Assert.Equal(task.Id, (await taskRepository.GetByStageAndKey(stage.Id, "inventories.reserve")).Id);
        Assert.Single(await taskRepository.GetByInstanceId(instanceId));
        Assert.Empty(await taskRepository.GetWaitingResponseOlderThan(now));
        Assert.Equal(attempt.Id, (await attemptRepository.GetById(attempt.Id)).Id);
        Assert.Equal(attempt.Id, (await attemptRepository.GetByDispatchId(dispatch.Id)).Id);
        Assert.Equal([attempt.Id], (await attemptRepository.GetByTaskExecutionId(task.Id)).Select(x => x.Id).ToArray());
        Assert.Empty(await attemptRepository.GetWaitingResponseOlderThan(now));
        Assert.Equal(compensation.Id, (await compensationRepository.TryGetById(compensation.Id))!.Id);
        Assert.Empty(await compensationRepository.GetPending());
        Assert.Single(await compensationRepository.GetByInstanceId(instanceId));
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, artifact.Status);
        Assert.Single(await artifactRepository.GetReady());
        Assert.Equal(artifact.Id, (await artifactRepository.GetByVersion("sales.sale.created", new SemanticVersion(1, 2, 0))).Id);
        Assert.Equal(2, (await ingressRepository.ReadActiveAsync(0, 10)).Count);
        var activeIngresses = await ingressRepository.GetActiveByArtifactIdAsync(artifact.Id);
        Assert.Equal(["backchannel", "trigger"], activeIngresses.Select(configuration => configuration.ConfigurationKey).ToArray());
        Assert.Equal("updated", JsonNode.Parse(activeIngresses.Last().SettingsPayload)!["topic"]!.GetValue<string>());
        await ingressRepository.DeactivateForArtifactsAsync(null!);
        await ingressRepository.DeactivateForArtifactsAsync([]);
        Assert.Equal(2, (await ingressRepository.ReadActiveAsync(0, 10)).Count);
        await ingressRepository.DeactivateForArtifactsAsync([artifact.Id]);
        Assert.Empty(await ingressRepository.ReadActiveAsync(0, 10));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => stageRepository.GetById(Id.New()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => taskRepository.GetById(Id.New()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => attemptRepository.GetById(Id.New()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => attemptRepository.GetByDispatchId(Id.New()));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        return services.BuildServiceProvider();
    }

    private static OrchestrationInstance Instance(
        OrchestrationInstanceStatus status,
        DateTime lastUpdatedOnUtc,
        DateTime? completedOnUtc = null,
        DateTime? failedOnUtc = null)
        => new()
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = Id.New().ToString(),
            SagaId = Id.New().ToString(),
            ExecutionKey = Id.New().ToString(),
            Status = status,
            StartedOnUtc = lastUpdatedOnUtc.AddMinutes(-1),
            LastUpdatedOnUtc = lastUpdatedOnUtc,
            CompletedOnUtc = completedOnUtc,
            FailedOnUtc = failedOnUtc,
            SnapshotPayload = JsonNode.Parse("{}")
        };

    private static TaskDispatch Dispatch(string commandId, DateTime scheduledOnUtc)
        => new()
        {
            Id = Id.New(),
            TaskExecutionAttemptId = Id.New(),
            DispatchType = "Messaging",
            Destination = "commands.sales.test",
            RequestPayload = JsonNode.Parse("""{"saleId":"sale-1"}"""),
            DispatchStatus = "Scheduled",
            CommandId = commandId,
            CorrelationId = "correlation-1",
            ScheduledOnUtc = scheduledOnUtc
        };
}
