using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class RuntimeEntityFrameworkRepositoryTests
{
    [Fact]
    public async Task RuntimeExecutionRepositoriesPersistQueryAndUpdateExecutionGraph()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        var artifactRepository = services.GetRequiredService<IRuntimeArtifactRepository>();
        var instanceRepository = services.GetRequiredService<IOrchestrationInstanceRepository>();
        var stageRepository = services.GetRequiredService<IStageExecutionRepository>();
        var taskRepository = services.GetRequiredService<ITaskExecutionRepository>();
        var attemptRepository = services.GetRequiredService<ITaskExecutionAttemptRepository>();
        var dispatchRepository = services.GetRequiredService<ITaskDispatchRepository>();
        var instanceVariableRepository = services.GetRequiredService<IInstanceVariableRepository>();
        var environmentVariableRepository = services.GetRequiredService<IEnvironmentVariableRepository>();
        var compensationRepository = services.GetRequiredService<ICompensationExecutionRepository>();
        var transitionRepository = services.GetRequiredService<IExecutionTransitionRepository>();
        var unitOfWork = services.GetRequiredService<IRuntimeStorageUnitOfWork>();

        var now = DateTime.UtcNow;
        var artifact = RuntimeArtifact(new SemanticVersion(2, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        await artifactRepository.Upsert(artifact);

        var instance = Instance(artifact.Id, OrchestrationInstanceStatus.Running, now.AddMinutes(-10));
        instance.Metadata["source"] = JsonValue.Create("runtime-ef")!;

        using (unitOfWork.DeferAutoSave())
        {
            await instanceRepository.Create(instance);
            await unitOfWork.SaveChanges();
        }

        var acquired = await instanceRepository.TryAcquireLease(
            instance.Id,
            "lease-1",
            now,
            now.AddMinutes(5));
        var blocked = await instanceRepository.TryAcquireLease(
            instance.Id,
            "lease-2",
            now.AddMinutes(1),
            now.AddMinutes(6));

        Assert.NotNull(acquired);
        Assert.Null(blocked);

        await instanceRepository.ReleaseLease(instance.Id, "wrong-lease");
        Assert.Equal("lease-1", (await instanceRepository.GetById(instance.Id)).ActiveLeaseId);
        await instanceRepository.ReleaseLease(instance.Id, "lease-1");
        Assert.Null((await instanceRepository.GetById(instance.Id)).ActiveLeaseId);

        var inventoryStage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "inventory-reservation",
            Order = 2,
            Status = StageExecutionStatus.Running,
            StartedOnUtc = now.AddMinutes(-9),
            Metadata = { ["runner"] = JsonValue.Create("ef")! }
        };
        var paymentStage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "payment-capture",
            Order = 1,
            Status = StageExecutionStatus.Pending
        };

        await stageRepository.Create(inventoryStage);
        await stageRepository.Create(paymentStage);
        inventoryStage.Status = StageExecutionStatus.Completed;
        inventoryStage.CompletedOnUtc = now.AddMinutes(-8);
        await stageRepository.Update(inventoryStage);

        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = inventoryStage.Id,
            TaskKey = "inventories.reserve",
            TaskKind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Status = TaskExecutionStatus.WaitingResponse,
            OnErrorPolicy = OnErrorPolicy.StopAndCompensate,
            AwaitResponse = true,
            StartedOnUtc = now.AddMinutes(-8),
            WaitingSinceUtc = now.AddMinutes(-7),
            LastAttemptNumber = 1,
            OutputVariablesPayload = JsonNode.Parse("""{"reserved":true}"""),
            CorrelationId = "task-correlation",
            Metadata = { ["operation"] = JsonValue.Create("reserve")! }
        };
        var laterTask = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = inventoryStage.Id,
            TaskKey = "inventories.commit",
            TaskKind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Status = TaskExecutionStatus.WaitingResponse,
            OnErrorPolicy = OnErrorPolicy.Stop,
            AwaitResponse = true,
            StartedOnUtc = now,
            WaitingSinceUtc = now.AddMinutes(-1)
        };

        await taskRepository.Create(task);
        await taskRepository.Create(laterTask);
        task.Status = TaskExecutionStatus.Completed;
        task.CompletedOnUtc = now.AddMinutes(-6);
        await taskRepository.Update(task);

        var dispatchId = Id.New();
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.WaitingResponse,
            StartedOnUtc = now.AddMinutes(-8),
            WaitingSinceUtc = now.AddMinutes(-7),
            RequestPayload = JsonNode.Parse("""{"sku":"ABC"}"""),
            ResponsePayload = JsonNode.Parse("""{"reserved":true}"""),
            DispatchId = dispatchId,
            Metadata = { ["transport"] = JsonValue.Create("messaging")! }
        };
        var laterAttempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = laterTask.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.WaitingResponse,
            WaitingSinceUtc = now.AddMinutes(5)
        };

        await attemptRepository.Create(attempt);
        await attemptRepository.Create(laterAttempt);
        attempt.Status = TaskExecutionStatus.Completed;
        attempt.CompletedOnUtc = now.AddMinutes(-6);
        await attemptRepository.Update(attempt);

        var dispatch = new TaskDispatch
        {
            Id = dispatchId,
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            Destination = "inventories.reserve",
            RequestPayload = JsonNode.Parse("""{"sku":"ABC"}"""),
            DispatchStatus = "Pending",
            CommandId = "cmd-1",
            CorrelationId = "task-correlation",
            ScheduledOnUtc = now.AddMinutes(-9),
            Metadata = { ["topic"] = JsonValue.Create("inventories.reserve")! }
        };
        var oldDispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = laterAttempt.Id,
            DispatchType = "Messaging",
            DispatchStatus = "Pending",
            CommandId = "cmd-2",
            ScheduledOnUtc = now.AddMinutes(-20)
        };

        await dispatchRepository.Create(dispatch);
        await dispatchRepository.Create(oldDispatch);
        await dispatchRepository.MarkSent(dispatch.Id, "Sent", now.AddMinutes(-8), "external-1");
        await dispatchRepository.MarkFailed(oldDispatch.Id, "broker down", "external-2");
        await dispatchRepository.Update(new TaskDispatch
        {
            Id = dispatch.Id,
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            Destination = "inventories.reserve",
            RequestPayload = JsonNode.Parse("""{"sku":"ABC","retry":true}"""),
            DispatchStatus = "Retried",
            CommandId = "cmd-1",
            CorrelationId = "task-correlation",
            ScheduledOnUtc = now.AddMinutes(-9),
            SentOnUtc = now.AddMinutes(-8),
            Metadata = { ["topic"] = JsonValue.Create("inventories.reserve")! }
        });

        var instanceVariable = new InstanceVariable
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            Key = "reserve.output",
            Scope = VariableScope.Instance,
            ValueType = VariableValueType.Json,
            Value = JsonNode.Parse("""{"reserved":true}"""),
            SourceType = "Task",
            SourceReference = task.TaskKey,
            CreatedOnUtc = now,
            LastUpdatedBy = "runtime"
        };
        await instanceVariableRepository.Upsert(instanceVariable);
        instanceVariable.Value = JsonNode.Parse("""{"reserved":true,"updated":true}""");
        await instanceVariableRepository.Upsert(instanceVariable);
        var environmentVariable = new EnvironmentVariableValue
        {
            Id = Id.New(),
            VariableKey = "inventories.timeout",
            ValueType = VariableValueType.TimeSpan,
            Value = JsonValue.Create("00:05:00"),
            IsResolved = true,
            CreatedOnUtc = now,
            UpdatedOnUtc = now,
            UpdatedBy = "tests"
        };
        await environmentVariableRepository.Upsert(environmentVariable);
        environmentVariable.Value = JsonValue.Create("00:10:00");
        await environmentVariableRepository.Upsert(environmentVariable);

        var compensation = new CompensationExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            SourceTaskExecutionId = task.Id,
            CompensationTaskKey = "inventories.release",
            Status = "Pending",
            StartedOnUtc = now.AddMinutes(-5),
            RequestPayload = JsonNode.Parse("""{"sku":"ABC"}""")
        };
        await compensationRepository.Create(compensation);
        compensation.Status = "Completed";
        compensation.CompletedOnUtc = now.AddMinutes(-4);
        await compensationRepository.Update(compensation);
        await compensationRepository.Create(new CompensationExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            SourceTaskExecutionId = laterTask.Id,
            CompensationTaskKey = "inventories.cancel",
            Status = "Pending",
            StartedOnUtc = now.AddMinutes(-3)
        });

        await transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = inventoryStage.Id,
            TaskExecutionId = task.Id,
            TaskExecutionAttemptId = attempt.Id,
            TransitionType = "TaskCompleted",
            FromStatus = "WaitingResponse",
            ToStatus = "Completed",
            OccurredOnUtc = now.AddMinutes(-6),
            Message = "Task completed",
            Payload = JsonNode.Parse("""{"ok":true}"""),
            ProducedBy = "tests"
        });

        instance.Status = OrchestrationInstanceStatus.Completed;
        instance.CompletedOnUtc = now.AddMinutes(-1);
        instance.LastUpdatedOnUtc = now.AddMinutes(-1);
        await instanceRepository.Update(instance);

        Assert.Equal("sales.sale.created", (await artifactRepository.GetActive("sales.sale.created")).OrchestrationDefinitionKey);
        Assert.Equal(inventoryStage.Id, (await stageRepository.GetById(inventoryStage.Id)).Id);
        Assert.Equal([paymentStage.Id, inventoryStage.Id], (await stageRepository.GetByInstanceId(instance.Id)).Select(x => x.Id).ToArray());
        Assert.Equal(inventoryStage.Id, (await stageRepository.GetByInstanceAndKey(instance.Id, "inventory-reservation")).Id);
        Assert.Equal(task.Id, (await taskRepository.GetById(task.Id)).Id);
        Assert.Equal(
            new[] { task.Id.ToString(), laterTask.Id.ToString() }.OrderBy(x => x).ToArray(),
            (await taskRepository.GetByInstanceId(instance.Id)).Select(x => x.Id.ToString()).OrderBy(x => x).ToArray());
        Assert.Equal(task.Id, (await taskRepository.GetByStageAndKey(inventoryStage.Id, "inventories.reserve")).Id);
        Assert.Equal(task.Id, (await taskRepository.GetByCorrelationId("task-correlation")).Id);
        Assert.Equal(laterTask.Id, Assert.Single(await taskRepository.GetWaitingResponseOlderThan(now)).Id);
        Assert.Equal(attempt.Id, (await attemptRepository.GetById(attempt.Id)).Id);
        Assert.Equal(attempt.Id, (await attemptRepository.GetByDispatchId(dispatch.Id)).Id);
        Assert.Equal([attempt.Id], (await attemptRepository.GetByTaskExecutionId(task.Id)).Select(x => x.Id).ToArray());
        Assert.Empty(await attemptRepository.GetWaitingResponseOlderThan(now));
        Assert.Equal(dispatch.Id, (await dispatchRepository.GetById(dispatch.Id)).Id);
        Assert.Equal(dispatch.Id, (await dispatchRepository.TryGetById(dispatch.Id)).Id);
        Assert.Equal(dispatch.Id, (await dispatchRepository.GetByCommandId("cmd-1")).Id);
        Assert.Equal(dispatch.Id, (await dispatchRepository.GetByAttemptId(attempt.Id)).Id);
        Assert.Equal(2, (await dispatchRepository.GetScheduledOlderThan(now)).Count);
        Assert.Equal(compensation.Id, (await compensationRepository.TryGetById(compensation.Id)).Id);
        Assert.Single(await instanceVariableRepository.GetByInstanceId(instance.Id));
        Assert.Equal("00:10:00", (await environmentVariableRepository.GetAll()).Single().Value!.ToString());
        Assert.Single(await compensationRepository.GetPending());
        Assert.Equal(2, (await compensationRepository.GetByInstanceId(instance.Id)).Count);
        Assert.Single(await transitionRepository.GetByInstanceId(instance.Id));
        Assert.Single(await transitionRepository.GetRecent(1));

        var summary = await instanceRepository.GetSummary(now.AddMinutes(-2));
        Assert.Equal(1, summary.CompletedRecent);
        Assert.Empty((await instanceRepository.GetRecent(1)).Where(x => x.Status != OrchestrationInstanceStatus.Completed));
    }

    [Fact]
    public async Task RuntimeArtifactIngressAccessorsAndDesignNodesUseEntityFrameworkStorage()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        var artifactRepository = services.GetRequiredService<IRuntimeArtifactRepository>();
        var ingressRepository = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var allIngressAccessor = services.GetRequiredService<IGetAllIngressConfigurationsAccessor>();
        var byArtifactAccessor = services.GetRequiredService<IGetIngressConfigurationByArtifactAccessor>();
        var designNodeRepository = services.GetRequiredService<IRuntimeDesignNodeRepository>();
        var sourceProvider = services.GetRequiredService<IControlPlaneDistributionSourceProvider>();

        var readyArtifact = RuntimeArtifact(new SemanticVersion(3, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var pendingArtifact = RuntimeArtifact(new SemanticVersion(3, 1, 0), RuntimeOrchestrationArtifactStatus.Pending);
        await artifactRepository.Upsert(readyArtifact);
        await artifactRepository.Upsert(pendingArtifact);

        var readyIngresses = Enumerable.Range(1, 501)
            .Select(index => Ingress(
                readyArtifact.Id,
                $"trigger-{index:000}",
                index % 2 == 0 ? IngressKind.Backchannel : IngressKind.Trigger))
            .ToArray();
        await ingressRepository.UpsertForArtifactAsync(readyArtifact.Id, readyIngresses);
        await ingressRepository.UpsertForArtifactAsync(pendingArtifact.Id, [Ingress(pendingArtifact.Id, "pending-trigger")]);

        var firstPage = await allIngressAccessor.ReadAsync();
        var secondPage = await allIngressAccessor.ReadAsync();
        var artifactIngresses = await byArtifactAccessor.GetConfigurationAsync(readyArtifact.Id.ToString());

        Assert.True(firstPage.HasMoreItems);
        Assert.Equal(500, firstPage.Configurations.Count);
        Assert.False(secondPage.HasMoreItems);
        Assert.Single(secondPage.Configurations);
        Assert.Equal(501, artifactIngresses.Count);
        Assert.All(artifactIngresses, ingress =>
        {
            Assert.Equal("sales.sale.created", ingress.OrchestrationDefinitionKey);
            Assert.Equal("3.0.0", ingress.OrchestrationVersion);
        });

        await ingressRepository.UpsertForArtifactAsync(readyArtifact.Id, [Ingress(readyArtifact.Id, "trigger-001", IngressKind.Trigger, """{"topic":"updated"}""")]);
        var activeAfterUpsert = await ingressRepository.GetActiveByArtifactIdAsync(readyArtifact.Id);
        Assert.Single(activeAfterUpsert);
        Assert.Equal("""{"topic":"updated"}""", activeAfterUpsert.Single().SettingsPayload);

        var sourceNode = DesignNode(
            "design-main",
            "Design Main",
            DistributionConnectionMode.HybridSync,
            RuntimeDesignNodeStatus.Enabled,
            isEnabled: true,
            outboundStatus: ConnectionCredentialStatus.Active);
        var pushOnlyNode = DesignNode(
            "design-push",
            "Design Push",
            DistributionConnectionMode.DesignPublishesToRuntime,
            RuntimeDesignNodeStatus.Enabled,
            isEnabled: true,
            outboundStatus: ConnectionCredentialStatus.Active);
        var disabledNode = DesignNode(
            "design-disabled",
            "Design Disabled",
            DistributionConnectionMode.RuntimeFetchesFromDesign,
            RuntimeDesignNodeStatus.Suspend,
            isEnabled: false,
            outboundStatus: ConnectionCredentialStatus.Active);

        await designNodeRepository.UpsertAsync(sourceNode);
        await designNodeRepository.UpsertAsync(pushOnlyNode);
        await designNodeRepository.UpsertAsync(disabledNode);
        await designNodeRepository.SetStatusAsync(disabledNode.Id, RuntimeDesignNodeStatus.Enabled);
        await designNodeRepository.SetEnabledAsync(disabledNode.Id, false);

        var enabledSources = sourceProvider.GetAll();
        var selectedByKey = sourceProvider.GetByKey("design-main");
        var selectedByClient = sourceProvider.GetByClientId("out-client-design-main");

        Assert.Equal(3, (await designNodeRepository.GetAllAsync()).Count);
        Assert.Equal(sourceNode.Id, (await designNodeRepository.GetByKeyAsync("design-main"))!.Id);
        Assert.Equal(sourceNode.Id, (await designNodeRepository.GetByInboundClientIdAsync("in-client-design-main"))!.Id);
        Assert.Single(enabledSources);
        Assert.Equal("design-main", selectedByKey.Key);
        Assert.Equal("out-client-design-main", selectedByClient.ClientId);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Task.FromResult(sourceProvider.GetByKey("missing")));
    }

    [Fact]
    public async Task RuntimeArtifactRepositoryTracksProjectionLifecycleAndRetiresPreviousArtifacts()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var artifactRepository = services.GetRequiredService<IRuntimeArtifactRepository>();
        var ingressRepository = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var dbContext = services.GetRequiredService<RuntimeDbContext>();
        var previous = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        previous.DeployedOnUtc = DateTime.UtcNow.AddMinutes(-20);
        var current = RuntimeArtifact(new SemanticVersion(2, 0, 0), RuntimeOrchestrationArtifactStatus.Pending);
        current.IngressGeneration = 3;
        current.DeployedOnUtc = DateTime.UtcNow;
        var otherKey = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        otherKey.OrchestrationDefinitionKey = "billing.invoice.created";
        otherKey.DeployedOnUtc = DateTime.UtcNow.AddMinutes(-10);

        await artifactRepository.Upsert(previous);
        await artifactRepository.Upsert(current);
        await artifactRepository.Upsert(otherKey);
        current.LoadedToCache = true;
        await artifactRepository.Upsert(current);
        await ingressRepository.UpsertForArtifactAsync(previous.Id, [Ingress(previous.Id, "previous-trigger")]);
        await ingressRepository.UpsertForArtifactAsync(current.Id, [Ingress(current.Id, "current-trigger")]);
        await ingressRepository.DeactivateForArtifactsAsync([], CancellationToken.None);

        await artifactRepository.MarkProjectionStarted(current.Id, ingressGeneration: 99);
        Assert.Null((await artifactRepository.GetById(current.Id)).ProjectionStartedOnUtc);

        await artifactRepository.MarkProjectionStarted(current.Id, current.IngressGeneration);
        var started = await artifactRepository.GetById(current.Id);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Pending, started.Status);
        Assert.NotNull(started.ProjectionStartedOnUtc);

        await artifactRepository.MarkProjectionFailed(current.Id, ingressGeneration: 99, "stale");
        Assert.Null((await artifactRepository.GetById(current.Id)).ProjectionFailedOnUtc);

        await artifactRepository.MarkProjectionFailed(current.Id, current.IngressGeneration, "projection failed");
        var failed = await artifactRepository.GetById(current.Id);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Failed, failed.Status);
        Assert.Equal("projection failed", failed.ProjectionError);
        Assert.NotNull(failed.ProjectionFailedOnUtc);

        await artifactRepository.MarkReady(current.Id, ingressGeneration: 99);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Failed, (await artifactRepository.GetById(current.Id)).Status);

        await artifactRepository.MarkReady(current.Id, current.IngressGeneration);
        var ready = await artifactRepository.GetById(current.Id);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, ready.Status);
        Assert.NotNull(ready.ActivatedOnUtc);
        Assert.NotNull(ready.ProjectionCompletedOnUtc);
        Assert.Null(ready.ProjectionFailedOnUtc);
        Assert.Null(ready.ProjectionError);

        await artifactRepository.DeactivateActiveArtifacts("sales.sale.created", current.Id);

        var retired = await artifactRepository.GetById(previous.Id);
        var activeCurrent = await artifactRepository.GetByVersion("sales.sale.created", new SemanticVersion(2, 0, 0));
        var activeOther = await artifactRepository.GetActive("billing.invoice.created");
        var readyArtifacts = await artifactRepository.GetReady();
        var allArtifacts = await artifactRepository.GetAll();
        var activeIngresses = await ingressRepository.ReadActiveAsync(0, 10);

        Assert.False(retired.IsActive);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Retired, retired.Status);
        Assert.NotNull(retired.RetiredOnUtc);
        Assert.Equal(current.Id, activeCurrent.Id);
        Assert.Equal(otherKey.Id, activeOther.Id);
        Assert.Contains(readyArtifacts, artifact => artifact.Id == current.Id);
        Assert.Contains(readyArtifacts, artifact => artifact.Id == otherKey.Id);
        Assert.Equal(current.Id, allArtifacts.First().Id);
        Assert.DoesNotContain(activeIngresses, ingress => ingress.RuntimeOrchestrationArtifactId == previous.Id);
        Assert.Contains(activeIngresses, ingress => ingress.RuntimeOrchestrationArtifactId == current.Id);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Retired, await dbContext.RuntimeOrchestrationArtifacts
            .Where(x => x.Id == previous.Id)
            .Select(x => x.Status)
            .SingleAsync());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => artifactRepository.GetByVersion("sales.sale.created", new SemanticVersion(9, 9, 9)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => artifactRepository.GetById(Id.New()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => artifactRepository.GetActive("missing.key"));
    }

    [Fact]
    public async Task RuntimeDesignNodeRepositoryUpsertUpdatesExistingNodeAndStatus()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRuntimeDesignNodeRepository>();
        var created = DesignNode(
            "design-main",
            "Design Main",
            DistributionConnectionMode.HybridSync,
            RuntimeDesignNodeStatus.Pending,
            isEnabled: false,
            outboundStatus: ConnectionCredentialStatus.Missing);

        await repository.UpsertAsync(created);

        var updated = DesignNode(
            "design-main-updated",
            "Design Main Updated",
            DistributionConnectionMode.RuntimeFetchesFromDesign,
            RuntimeDesignNodeStatus.Enabled,
            isEnabled: true,
            outboundStatus: ConnectionCredentialStatus.Active);
        updated.Id = created.Id;
        updated.EndpointBaseUri = "https://design-updated.local";
        updated.RemoteRuntimeNodeId = "remote-updated";
        updated.AccessTokenTtlSeconds = 600;
        updated.TokenRefreshSkewSeconds = 60;
        updated.TokenValidationCacheTtlSeconds = 30;
        updated.InboundClientId = "in-client-updated";
        updated.InboundKeyId = "in-key-updated";
        updated.InboundSecretHash = "updated-hash";
        updated.InboundAllowedScopes = "artifact:push artifact:read";
        updated.InboundCredentialStatus = ConnectionCredentialStatus.Revoked;
        updated.InboundCredentialCreatedAtUtc = DateTime.UtcNow.AddDays(-3);
        updated.InboundCredentialRotatedAtUtc = DateTime.UtcNow.AddDays(-2);
        updated.InboundCredentialRevokedAtUtc = DateTime.UtcNow.AddDays(-1);
        updated.InboundLastTokenIssuedAtUtc = DateTime.UtcNow.AddHours(-4);
        updated.InboundLastTokenFailedAtUtc = DateTime.UtcNow.AddHours(-3);
        updated.InboundLastFailureReason = "bad secret";
        updated.OutboundClientId = "out-client-updated";
        updated.OutboundKeyId = "out-key-updated";
        updated.ProtectedOutboundSecret = "protected-updated";
        updated.OutboundRequestedScopes = "release:read";
        updated.OutboundCredentialImportedAtUtc = DateTime.UtcNow.AddHours(-2);
        updated.OutboundLastTokenReceivedAtUtc = DateTime.UtcNow.AddHours(-1);
        updated.Description = "updated description";
        updated.CreatedOnUtc = DateTime.UtcNow.AddDays(-10);
        updated.UpdatedOnUtc = DateTime.UtcNow;

        await repository.UpsertAsync(updated);
        await repository.SetStatusAsync(created.Id, RuntimeDesignNodeStatus.Suspend);
        await repository.SetEnabledAsync(created.Id, true);
        await repository.SetStatusAsync(Id.New(), RuntimeDesignNodeStatus.Enabled);

        var stored = await repository.GetByIdAsync(created.Id);

        Assert.NotNull(stored);
        Assert.Equal("design-main-updated", stored.Key);
        Assert.Equal("Design Main Updated", stored.Name);
        Assert.Equal("https://design-updated.local", stored.EndpointBaseUri);
        Assert.Equal("remote-updated", stored.RemoteRuntimeNodeId);
        Assert.Equal(DistributionConnectionMode.RuntimeFetchesFromDesign, stored.DistributionMode);
        Assert.Equal(600, stored.AccessTokenTtlSeconds);
        Assert.Equal(60, stored.TokenRefreshSkewSeconds);
        Assert.Equal(30, stored.TokenValidationCacheTtlSeconds);
        Assert.Equal("in-client-updated", stored.InboundClientId);
        Assert.Equal("in-key-updated", stored.InboundKeyId);
        Assert.Equal("updated-hash", stored.InboundSecretHash);
        Assert.Equal("artifact:push artifact:read", stored.InboundAllowedScopes);
        Assert.Equal(ConnectionCredentialStatus.Revoked, stored.InboundCredentialStatus);
        Assert.NotNull(stored.InboundCredentialRotatedAtUtc);
        Assert.NotNull(stored.InboundCredentialRevokedAtUtc);
        Assert.NotNull(stored.InboundLastTokenIssuedAtUtc);
        Assert.NotNull(stored.InboundLastTokenFailedAtUtc);
        Assert.Equal("bad secret", stored.InboundLastFailureReason);
        Assert.Equal("out-client-updated", stored.OutboundClientId);
        Assert.Equal("out-key-updated", stored.OutboundKeyId);
        Assert.Equal("protected-updated", stored.ProtectedOutboundSecret);
        Assert.Equal("release:read", stored.OutboundRequestedScopes);
        Assert.Equal(ConnectionCredentialStatus.Active, stored.OutboundCredentialStatus);
        Assert.NotNull(stored.OutboundCredentialImportedAtUtc);
        Assert.NotNull(stored.OutboundLastTokenReceivedAtUtc);
        Assert.Equal("updated description", stored.Description);
        Assert.Equal(RuntimeDesignNodeStatus.Enabled, stored.Status);
        Assert.True(stored.IsEnabled);
        Assert.Empty(repository.GetEnabled().Where(x => x.Id == Id.New()));
    }

    [Fact]
    public async Task ExecutionTransitionRepositoryPublishesReactiveEventsForRuntimeTimeline()
    {
        var publisher = new RecordingRuntimeReactiveEventPublisher();
        await using var provider = CreateProvider(publisher);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var artifactRepository = services.GetRequiredService<IRuntimeArtifactRepository>();
        var instanceRepository = services.GetRequiredService<IOrchestrationInstanceRepository>();
        var stageRepository = services.GetRequiredService<IStageExecutionRepository>();
        var taskRepository = services.GetRequiredService<ITaskExecutionRepository>();
        var transitionRepository = services.GetRequiredService<IExecutionTransitionRepository>();
        var now = DateTime.UtcNow;
        var artifact = RuntimeArtifact(new SemanticVersion(4, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        await artifactRepository.Upsert(artifact);
        var instance = Instance(artifact.Id, OrchestrationInstanceStatus.Running, now.AddMinutes(-5));
        await instanceRepository.Create(instance);
        var stage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "inventory",
            Order = 1,
            Status = StageExecutionStatus.Running,
            StartedOnUtc = now.AddMinutes(-4)
        };
        await stageRepository.Create(stage);
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = stage.Id,
            TaskKey = "inventories.reserve",
            TaskKind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Status = TaskExecutionStatus.Running,
            OnErrorPolicy = OnErrorPolicy.Stop,
            StartedOnUtc = now.AddMinutes(-3)
        };
        await taskRepository.Create(task);

        var expectedNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InstancePromoted"] = RuntimeReactiveEventNames.OrchestrationStarted,
            ["InstanceStarted"] = RuntimeReactiveEventNames.OrchestrationStarted,
            ["InstanceWaitingResponse"] = RuntimeReactiveEventNames.OrchestrationWaiting,
            ["InstanceCompensating"] = RuntimeReactiveEventNames.OrchestrationCompensating,
            ["InstanceCompleted"] = RuntimeReactiveEventNames.OrchestrationCompleted,
            ["InstanceCompensated"] = RuntimeReactiveEventNames.OrchestrationCompensated,
            ["InstanceFailed"] = RuntimeReactiveEventNames.OrchestrationFailed,
            ["StageStarted"] = RuntimeReactiveEventNames.StageStarted,
            ["StageCompleted"] = RuntimeReactiveEventNames.StageCompleted,
            ["StageFailed"] = RuntimeReactiveEventNames.StageFailed,
            ["StageSkipped"] = RuntimeReactiveEventNames.StageSkipped,
            ["TaskStarted"] = RuntimeReactiveEventNames.TaskStarted,
            ["TaskInputTransformed"] = RuntimeReactiveEventNames.TaskInputTransformed,
            ["TaskCompleted"] = RuntimeReactiveEventNames.TaskCompleted,
            ["TaskCallbackCompleted"] = RuntimeReactiveEventNames.TaskCompleted,
            ["TaskFailed"] = RuntimeReactiveEventNames.TaskFailed,
            ["TaskCallbackFailed"] = RuntimeReactiveEventNames.TaskFailed,
            ["TaskSkipped"] = RuntimeReactiveEventNames.TaskSkipped,
            ["TaskWaitingResponse"] = RuntimeReactiveEventNames.TaskWaiting,
            ["TaskRetryScheduled"] = RuntimeReactiveEventNames.TaskRetryScheduled,
            ["TaskRetryStarted"] = RuntimeReactiveEventNames.TaskRetryStarted,
            ["TaskTimedOut"] = RuntimeReactiveEventNames.TaskTimedOut,
            ["DispatchPublished"] = RuntimeReactiveEventNames.DispatchPublished,
            ["TaskDispatched"] = RuntimeReactiveEventNames.DispatchPublished,
            ["DispatchFailed"] = RuntimeReactiveEventNames.DispatchFailed,
            ["CompensationScheduled"] = RuntimeReactiveEventNames.CompensationScheduled,
            ["CompensationStarted"] = RuntimeReactiveEventNames.CompensationStarted,
            ["CompensationDispatched"] = RuntimeReactiveEventNames.CompensationDispatched,
            ["CompensationCompleted"] = RuntimeReactiveEventNames.CompensationCompleted,
            ["CompensationFailed"] = RuntimeReactiveEventNames.CompensationFailed,
            ["ParallelGroupStarted"] = RuntimeReactiveEventNames.ParallelGroupStarted,
            ["ParallelGroupCompleted"] = RuntimeReactiveEventNames.ParallelGroupCompleted,
            ["ParallelGroupFailed"] = RuntimeReactiveEventNames.ParallelGroupFailed,
            ["UnknownTransition"] = RuntimeReactiveEventNames.TransitionRecorded
        };

        foreach (var transitionType in expectedNames.Keys)
        {
            await transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = stage.Id,
                TaskExecutionId = task.Id,
                TransitionType = transitionType,
                FromStatus = "Before",
                ToStatus = "After",
                OccurredOnUtc = now,
                Message = transitionType,
                Payload = JsonNode.Parse("""{"ok":true}"""),
                ProducedBy = "tests"
            });
        }

        await transitionRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = Id.New(),
            TransitionType = "TaskCompleted",
            OccurredOnUtc = now,
            ProducedBy = "tests"
        });

        Assert.Equal(expectedNames.Count, publisher.Events.Count);
        foreach (var (transitionType, eventName) in expectedNames)
        {
            var published = Assert.Single(publisher.Events, item => item.TransitionType == transitionType);
            Assert.Equal(eventName, published.EventName);
            Assert.Equal("sales.sale.created", published.OrchestrationDefinitionKey);
            Assert.Equal("4.0.0", published.OrchestrationVersion);
            Assert.Equal("inventory", published.StageKey);
            Assert.Equal("inventories.reserve", published.TaskKey);
            Assert.Equal(instance.CorrelationId, published.CorrelationId);
            Assert.Equal(instance.SagaId, published.SagaId);
        }
    }

    [Fact]
    public async Task ExecutionTransitionRepositoryBuildsHourlyTrafficBucketsWithTerminalStates()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var artifactRepository = services.GetRequiredService<IRuntimeArtifactRepository>();
        var instanceRepository = services.GetRequiredService<IOrchestrationInstanceRepository>();
        var transitionRepository = services.GetRequiredService<IExecutionTransitionRepository>();
        var now = DateTime.UtcNow;
        var artifact = RuntimeArtifact(new SemanticVersion(4, 1, 0), RuntimeOrchestrationArtifactStatus.Ready);
        await artifactRepository.Upsert(artifact);
        var running = Instance(artifact.Id, OrchestrationInstanceStatus.Running, now.AddHours(-3));
        var completed = Instance(artifact.Id, OrchestrationInstanceStatus.Completed, now.AddHours(-2).AddMinutes(-10));
        completed.CompletedOnUtc = now.AddHours(-1).AddMinutes(-10);
        var failed = Instance(artifact.Id, OrchestrationInstanceStatus.Failed, now.AddHours(-1).AddMinutes(-30));
        failed.FailedOnUtc = now.AddMinutes(-50);
        var compensated = Instance(artifact.Id, OrchestrationInstanceStatus.Compensated, now.AddHours(-2));
        compensated.CompensatedOnUtc = now.AddMinutes(-20);
        var stopped = Instance(artifact.Id, OrchestrationInstanceStatus.Stopped, now.AddHours(-2));
        stopped.StoppedOnUtc = now.AddMinutes(-15);
        await instanceRepository.Create(running);
        await instanceRepository.Create(completed);
        await instanceRepository.Create(failed);
        await instanceRepository.Create(compensated);
        await instanceRepository.Create(stopped);

        var traffic = await transitionRepository.GetTraffic(now.AddHours(-3));

        Assert.NotEmpty(traffic);
        Assert.Contains(traffic, point => point.Started > 0);
        Assert.Contains(traffic, point => point.Completed > 0);
        Assert.Contains(traffic, point => point.Failed > 0);
        Assert.Contains(traffic, point => point.Active >= 3);
    }

    private static ServiceProvider CreateProvider(IRuntimeReactiveEventPublisher? publisher = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options =>
            options.UseInMemoryDatabase($"runtime-storage-{Guid.NewGuid():N}"));
        if (publisher is not null)
        {
            services.Replace(ServiceDescriptor.Singleton(publisher));
        }

        return services.BuildServiceProvider();
    }

    private static RuntimeOrchestrationArtifact RuntimeArtifact(
        SemanticVersion version,
        RuntimeOrchestrationArtifactStatus status)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = version,
            ArtifactChecksum = new Checksum($"checksum-{version}"),
            ArtifactPayload = JsonNode.Parse("""{"definitionKey":"sales.sale.created"}""")!,
            Status = status,
            IngressGeneration = 1,
            IsActive = true,
            LoadedToCache = false,
            DeployedOnUtc = DateTime.UtcNow,
        };

    private static OrchestrationInstance Instance(
        Id artifactId,
        OrchestrationInstanceStatus status,
        DateTime startedOnUtc)
        => new()
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = artifactId,
            TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "correlation-ef",
            SagaId = "saga-ef",
            ExecutionKey = "sales.sale.created:correlation-ef",
            Status = status,
            StartedOnUtc = startedOnUtc,
            LastUpdatedOnUtc = startedOnUtc,
            SnapshotPayload = JsonNode.Parse("""{"saleId":"S-1"}"""),
        };

    private static RuntimeIngressConfiguration Ingress(
        Id artifactId,
        string key,
        IngressKind kind = IngressKind.Trigger,
        string settingsPayload = """{"topic":"events.sales.sale.created"}""")
        => new()
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = artifactId,
            ConfigurationKey = key,
            IngressKind = kind,
            IngressTransport = IngressTransport.Messaging,
            SettingsPayload = settingsPayload,
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
        };

    private static RuntimeDesignNode DesignNode(
        string key,
        string name,
        DistributionConnectionMode mode,
        RuntimeDesignNodeStatus status,
        bool isEnabled,
        ConnectionCredentialStatus outboundStatus)
        => new()
        {
            Id = Id.New(),
            Key = key,
            Name = name,
            EndpointBaseUri = $"https://{key}.local",
            RemoteRuntimeNodeId = $"remote-{key}",
            DistributionMode = mode,
            AccessTokenTtlSeconds = 86400,
            TokenRefreshSkewSeconds = 300,
            TokenValidationCacheTtlSeconds = 300,
            InboundClientId = $"in-client-{key}",
            InboundKeyId = $"in-key-{key}",
            InboundSecretHash = "hash",
            InboundAllowedScopes = "artifact:push",
            InboundCredentialStatus = ConnectionCredentialStatus.Active,
            InboundCredentialCreatedAtUtc = DateTime.UtcNow,
            OutboundClientId = $"out-client-{key}",
            OutboundKeyId = $"out-key-{key}",
            ProtectedOutboundSecret = "protected",
            OutboundRequestedScopes = "release:read artifact:read artifact:ack",
            OutboundCredentialStatus = outboundStatus,
            OutboundCredentialImportedAtUtc = DateTime.UtcNow,
            Description = "node for tests",
            Status = status,
            IsEnabled = isEnabled,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
        };

    private sealed class RecordingRuntimeReactiveEventPublisher : IRuntimeReactiveEventPublisher
    {
        public List<RuntimeReactiveEvent> Events { get; } = [];

        public Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
        {
            Events.Add(eventData);
            return Task.CompletedTask;
        }
    }
}
