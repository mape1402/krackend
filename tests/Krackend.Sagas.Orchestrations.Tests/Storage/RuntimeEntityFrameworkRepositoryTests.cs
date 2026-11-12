using System.Text.Json.Nodes;
using System.IO;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Extensions;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mule;
using Mule.Dispatching;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class RuntimeEntityFrameworkRepositoryTests
{
    [Fact]
    public async Task DueTimeoutsAreNotStarvedByOlderWaitsWithoutDeadlines()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var instanceId = Id.New();
        var stageId = Id.New();
        db.TaskExecutions.AddRange(Enumerable.Range(0, 100).Select(i => new TaskExecution
        {
            Id = Id.New(), OrchestrationInstanceId = instanceId, StageExecutionId = stageId,
            TaskKey = $"no-deadline-{i}", Status = TaskExecutionStatus.WaitingResponse,
            WaitingSinceUtc = now.AddHours(-2)
        }));
        var due = Enumerable.Range(0, 2).Select(i => new TaskExecution
        {
            Id = Id.New(), OrchestrationInstanceId = instanceId, StageExecutionId = stageId,
            TaskKey = $"parallel-{i}", Status = TaskExecutionStatus.WaitingResponse,
            WaitingSinceUtc = now.AddMinutes(-2), ResponseDeadlineUtc = now.AddMinutes(-1)
        }).ToArray();
        db.TaskExecutions.AddRange(due);
        await db.SaveChangesAsync();

        var tasks = scope.ServiceProvider.GetRequiredService<ITaskExecutionRepository>();
        for (var scan = 0; scan < 2; scan++)
        {
            var page = await tasks.GetResponseTimeoutsDue(now, take: 100);
            Assert.All(due, task => Assert.Contains(page, candidate => candidate.Id == task.Id));
        }
    }

    [Fact]
    public async Task CatalogFiltersProjectedScopeStatusAndUtcDatesBeforePaging()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var cases = new[]
        {
            ("orders", "1.0.0", OrchestrationInstanceStatus.Completed, start),
            ("orders", "1.0.0", OrchestrationInstanceStatus.Compensated, start.AddHours(2)),
            ("orders", "2.0.0", OrchestrationInstanceStatus.Completed, start.AddHours(3)),
            ("returns", "1.0.0", OrchestrationInstanceStatus.Completed, start.AddHours(4)),
            ("orders", "1.0.0", OrchestrationInstanceStatus.Running, start.AddHours(5)),
            ("orders", "1.0.0", OrchestrationInstanceStatus.Completed, start.AddDays(1))
        };
        foreach (var (key, version, status, time) in cases)
        {
            var instance = Instance(Id.New(), status, time);
            instance.OrchestrationDefinitionKey = key; instance.OrchestrationVersion = version;
            instance.Metadata["TriggerTraceId"] = JsonValue.Create("trace-" + time.Hour);
            await repository.Create(instance);
        }
        var catalog = Assert.IsAssignableFrom<IRuntimeInstanceCatalogRepository>(repository);
        var query = new RuntimeInstanceCatalogQuery(1, 1, Sort: RuntimeInstanceCatalogSort.Started, Descending: false,
            StartedFromUtc: start, StartedBeforeUtc: start.AddDays(1))
        {
            OrchestrationKey = " orders ", OrchestrationVersion = " 1.0.0 ",
            Statuses = [OrchestrationInstanceStatus.Completed, OrchestrationInstanceStatus.Compensated]
        };
        var first = await catalog.GetPage(query);
        var second = await catalog.GetPage(query with { PageNumber = 2 });
        Assert.Equal(2, first.TotalCount); Assert.Equal(2, second.TotalCount);
        Assert.Equal(start, Assert.Single(first.Items).StartedOnUtc);
        Assert.Equal("trace-0", Assert.Single(first.Items).Metadata["TriggerTraceId"]!.GetValue<string>());
        Assert.Equal(start.AddHours(2), Assert.Single(second.Items).StartedOnUtc);
    }

    [Fact]
    public async Task FailureScopeBackfillIsBoundedAndDoesNotMisattributeUnknownEvents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var repo = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow.AddMinutes(-2);
        var first = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddMinutes(-1)); first.OrchestrationDefinitionKey = "orders"; first.OrchestrationVersion = "1.0.0";
        var second = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddMinutes(-1)); second.OrchestrationDefinitionKey = "orders"; second.OrchestrationVersion = "2.0.0";
        var third = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddMinutes(-1)); third.OrchestrationDefinitionKey = "returns"; third.OrchestrationVersion = "1.0.0";
        var unknownVersion = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddMinutes(-1)); unknownVersion.OrchestrationDefinitionKey = "orders";
        foreach (var instance in new[] { first, second, third, unknownVersion }) await repo.Create(instance);
        foreach (var id in new[] { first.Id, second.Id, third.Id, unknownVersion.Id, Id.New() })
            db.ExecutionTransitions.Add(new ExecutionTransition { Id = Id.New(), OrchestrationInstanceId = id, OccurredOnUtc = now, TransitionType = "TaskTimedOut" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var eventBackfill = scope.ServiceProvider.GetRequiredService<ISagaFailureStatisticsBackfill>();
        for (var i = 0; i < 10 && !await eventBackfill.ProcessAsync(2); i++) { }
        var reader = Assert.IsAssignableFrom<ISagaFailureScopedStatisticsReader>(scope.ServiceProvider.GetRequiredService<ISagaFailureStatisticsReader>());
        var before = await reader.GetFailuresAsync(now.AddMinutes(-1), now.AddMinutes(1), "orders", "1.0.0");
        Assert.False(before.HistoryComplete); Assert.Equal(5, before.UnclassifiedCount);
        var backfill = scope.ServiceProvider.GetRequiredService<ISagaFailureScopeBackfill>();
        Assert.False(await backfill.ProcessAsync(2));
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM SagaFailureEvents WHERE ScopeProjectionVersion = 1").SingleAsync());
        for (var i = 0; i < 5 && !await backfill.ProcessAsync(2); i++) { }
        var scoped = await reader.GetFailuresAsync(now.AddMinutes(-1), now.AddMinutes(1), "orders", "1.0.0");
        Assert.True(scoped.HistoryComplete); Assert.Equal(1, Assert.Single(scoped.Items).Count); Assert.Equal(2, scoped.UnclassifiedCount);
        var allVersions = await reader.GetFailuresAsync(now.AddMinutes(-1), now.AddMinutes(1), "orders", null);
        Assert.Equal(3, Assert.Single(allVersions.Items).Count); Assert.Equal(1, allVersions.UnclassifiedCount);
        Assert.True(await backfill.ProcessAsync(2));
        db.ChangeTracker.Clear();
        var transitions = scope.ServiceProvider.GetRequiredService<IExecutionTransitionRepository>();
        await transitions.Create(new ExecutionTransition { Id = Id.New(), OrchestrationInstanceId = first.Id, OccurredOnUtc = now, TransitionType = "TaskCallbackFailed" });
        var latest = await reader.GetFailuresAsync(now.AddMinutes(-1), now.AddMinutes(1), "orders", "1.0.0");
        Assert.Equal(2, latest.Items.Sum(x => x.Count)); Assert.Equal(2, latest.UnclassifiedCount);
    }

    [Fact]
    public async Task FailureProjectionRecordsNewEventsAndBackfillsHistoricalEventsInBoundedPages()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow.AddHours(-1));
        await scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>().Create(instance);
        var timestamp = DateTime.UtcNow.AddMinutes(-10);
        db.ExecutionTransitions.AddRange(Enumerable.Range(0, 5).Select(_ => new ExecutionTransition
        {
            Id = Id.New(), OrchestrationInstanceId = instance.Id, OccurredOnUtc = timestamp, TransitionType = "TaskTimedOut"
        }));
        db.ExecutionTransitions.Add(new ExecutionTransition { Id = Id.New(), OrchestrationInstanceId = instance.Id,
            OccurredOnUtc = timestamp, TransitionType = "TaskCompleted" });
        await db.SaveChangesAsync();
        var transitions = scope.ServiceProvider.GetRequiredService<IExecutionTransitionRepository>();
        await transitions.Create(new ExecutionTransition { Id = Id.New(), OrchestrationInstanceId = instance.Id,
            OccurredOnUtc = timestamp, TransitionType = "TaskCallbackFailed" });
        var reader = scope.ServiceProvider.GetRequiredService<ISagaFailureStatisticsReader>();
        var before = await reader.GetFailuresAsync(timestamp.AddSeconds(-1), timestamp.AddSeconds(1));
        Assert.False(before.HistoryComplete);
        Assert.Equal(1, Assert.Single(before.Items).Count);
        var backfill = scope.ServiceProvider.GetRequiredService<ISagaFailureStatisticsBackfill>();
        for (var i = 0; i < 10 && !await backfill.ProcessAsync(2); i++) { }
        var report = await reader.GetFailuresAsync(timestamp.AddSeconds(-1), timestamp.AddSeconds(1));
        Assert.True(report.HistoryComplete);
        Assert.Equal(5, Assert.Single(report.Items.Where(x => x.Category == "Timeout")).Count);
        Assert.Equal(1, Assert.Single(report.Items.Where(x => x.Category == "Callback failure")).Count);
        Assert.Equal(6, report.Items.Sum(x => x.Count));
        Assert.True(await backfill.ProcessAsync(2));
    }

    [Fact]
    public async Task UpdatingFreshInstanceIgnoresAnOlderLocallyTrackedCopyButRejectsStaleWrites()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        await firstScope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var first = firstScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var original = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await first.Create(original);
        var stale = await first.GetById(original.Id);
        var updated = await second.GetById(original.Id);
        updated.ErrorSummary = "second scope";
        await second.Update(updated);
        var fresh = await first.GetById(original.Id);
        fresh.ErrorSummary = "fresh copy";

        await first.Update(fresh);

        Assert.Equal("fresh copy", (await second.GetById(original.Id)).ErrorSummary);
        stale.ErrorSummary = "stale copy";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.Update(stale));
    }

    [Fact]
    public async Task UnsupportedEfProviderDoesNotPretendToAcquireAnAtomicLease()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var instances = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await instances.Create(instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            instances.TryAcquireLease(instance.Id, "owner", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)));
        Assert.Null((await instances.GetById(instance.Id)).ActiveLeaseId);
    }

    [Fact]
    public async Task ArchivedRecoveryCleanupChecksInstancesPerBatchRatherThanPerVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var commands = new List<string>();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted]));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifacts = services.GetRequiredService<IRuntimeArtifactRepository>();
        for (var i = 1; i <= 20; i++)
        {
            var artifact = RuntimeArtifact(new SemanticVersion(i, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
            artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived;
            await artifacts.Upsert(artifact);
            db.RuntimeIngressConfigurations.Add(new RuntimeIngressConfiguration
            {
                Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, ConfigurationKey = "backchannel",
                IngressKind = IngressKind.Backchannel, SettingsPayload = "{}", IsActive = true
            });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        commands.Clear();
        var processor = new ArchivedRecoveryRetirementProcessor(artifacts, services.GetRequiredService<IOrchestrationInstanceRepository>(),
            services.GetRequiredService<IRuntimeStorageUnitOfWork>(), Substitute.For<IIngressRegistry>(),
            Options.Create(new OrchestrationTimeoutOptions { InstanceLifetimeBatchSize = 10 }),
            Options.Create(new OrchestrationCoordinationOptions()), NullLogger<ArchivedRecoveryRetirementProcessor>.Instance);

        Assert.Equal(20, await processor.ProcessAsync());
        Assert.Equal(4, commands.Count(x => x.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
            x.Contains("FROM \"OrchestrationInstances\"", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task EachReplicaClosesItsOwnConnectorAfterArchivedRecoveryIsRetired()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddKrackendOrchestrationsRuntime();
        registrations.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite(connection));
        var connector = Substitute.For<IIngressConector>();
        registrations.RemoveAllKeyed<IIngressConector>(IngressTransport.Messaging);
        registrations.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, (_, _) => connector);
        await using var provider = registrations.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var db = firstScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifact = RuntimeArtifact(new(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived;
        artifact.IngressGeneration = 5;
        await firstScope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>().Upsert(artifact);
        db.RuntimeIngressConfigurations.Add(new RuntimeIngressConfiguration
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, ConfigurationKey = "backchannel",
            IngressKind = IngressKind.Backchannel, IngressTransport = IngressTransport.Messaging, SettingsPayload = "{}", IsActive = true
        });
        await db.SaveChangesAsync();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var admission = provider.GetRequiredService<IRuntimeAdmissionController>();
        var firstRegistry = new IngressRegistry(scopeFactory, new RuntimeIngressLocalState(), admission);
        var secondRegistry = new IngressRegistry(scopeFactory, new RuntimeIngressLocalState(), admission);
        await firstRegistry.StandUpOneAsync(artifact.Id.ToString(), 5);
        await secondRegistry.StandUpOneAsync(artifact.Id.ToString(), 5);
        ArchivedRecoveryRetirementProcessor Processor(IServiceProvider services, IIngressRegistry registry) => new(
            services.GetRequiredService<IRuntimeArtifactRepository>(), services.GetRequiredService<IOrchestrationInstanceRepository>(),
            services.GetRequiredService<IRuntimeStorageUnitOfWork>(), registry, Options.Create(new OrchestrationTimeoutOptions()),
            Options.Create(new OrchestrationCoordinationOptions()), NullLogger<ArchivedRecoveryRetirementProcessor>.Instance);

        Assert.Equal(1, await Processor(firstScope.ServiceProvider, firstRegistry).ProcessAsync());
        Assert.Empty(firstRegistry.GetLocalArtifactIds());
        Assert.Single(secondRegistry.GetLocalArtifactIds());
        Assert.Equal(0, await Processor(secondScope.ServiceProvider, secondRegistry).ProcessAsync());
        Assert.Empty(secondRegistry.GetLocalArtifactIds());
        await connector.Received(2).DisconnectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchivedRecoveryCleanupRetiresOnlyWhenNoInstanceIsRunningOrLeased()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifacts = services.GetRequiredService<IRuntimeArtifactRepository>();
        var instances = services.GetRequiredService<IOrchestrationInstanceRepository>();
        var versions = Enumerable.Range(1, 3).Select(i => RuntimeArtifact(new SemanticVersion(i, 0, 0), RuntimeOrchestrationArtifactStatus.Ready)).ToArray();
        foreach (var artifact in versions)
        {
            artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived;
            artifact.IsActive = true;
            artifact.IngressGeneration = 5;
            await artifacts.Upsert(artifact);
            db.RuntimeIngressConfigurations.Add(new RuntimeIngressConfiguration
            {
                Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, ConfigurationKey = "backchannel",
                IngressKind = IngressKind.Backchannel, SettingsPayload = "{}", IsActive = true
            });
        }
        await db.SaveChangesAsync();
        var running = Instance(versions[1].Id, OrchestrationInstanceStatus.Running, DateTime.UtcNow.AddMinutes(-2));
        var leased = Instance(versions[2].Id, OrchestrationInstanceStatus.DeadLettered, DateTime.UtcNow.AddDays(-2));
        await instances.Create(running);
        await instances.Create(leased);
        Assert.NotNull(await instances.TryAcquireLease(leased.Id, "recovery-test", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)));
        var registry = Substitute.For<IIngressRegistry, IArchivedRecoveryIngressCleanup>();
        var cleanup = (IArchivedRecoveryIngressCleanup)registry;
        cleanup.GetLocalArtifactIds().Returns(versions.Select(x => x.Id.ToString()).ToArray());
        cleanup.ShutDownIfArchivedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        var disabledProcessor = new ArchivedRecoveryRetirementProcessor(artifacts, instances, services.GetRequiredService<IRuntimeStorageUnitOfWork>(), registry,
            Options.Create(new OrchestrationTimeoutOptions { InstanceLifetimeBatchSize = 10 }),
            Options.Create(new OrchestrationCoordinationOptions { Enabled = false }), NullLogger<ArchivedRecoveryRetirementProcessor>.Instance);
        Assert.Equal(0, await disabledProcessor.ProcessAsync());
        Assert.All(await db.RuntimeIngressConfigurations.AsNoTracking().ToArrayAsync(), x => Assert.True(x.IsActive));
        var processor = new ArchivedRecoveryRetirementProcessor(artifacts, instances, services.GetRequiredService<IRuntimeStorageUnitOfWork>(), registry,
            Options.Create(new OrchestrationTimeoutOptions { InstanceLifetimeBatchSize = 10 }),
            Options.Create(new OrchestrationCoordinationOptions()), NullLogger<ArchivedRecoveryRetirementProcessor>.Instance);

        Assert.Equal(1, await processor.ProcessAsync());
        Assert.False((await artifacts.GetById(versions[0].Id)).IsActive);
        Assert.True((await artifacts.GetById(versions[1].Id)).IsActive);
        Assert.True((await artifacts.GetById(versions[2].Id)).IsActive);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.RuntimeOrchestrationArtifactId == versions[0].Id)).IsActive);
        await cleanup.Received(1).ShutDownIfArchivedAsync(versions[0].Id.ToString(), Arg.Any<CancellationToken>());
        await cleanup.DidNotReceive().ShutDownIfArchivedAsync(versions[1].Id.ToString(), Arg.Any<CancellationToken>());

        var current = await instances.GetById(running.Id);
        current.Status = OrchestrationInstanceStatus.Completed;
        await instances.Update(current);
        await instances.ReleaseLease(leased.Id, "recovery-test");
        Assert.Equal(2, await processor.ProcessAsync());
        Assert.All(await db.RuntimeIngressConfigurations.AsNoTracking().ToArrayAsync(), x => Assert.False(x.IsActive));
        Assert.Equal(0, await processor.ProcessAsync());
    }

    [Fact]
    public async Task ArchiveWaitsForInFlightRecoveryLeaseAndSucceedsAfterItIsReleased()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifacts = services.GetRequiredService<IRuntimeArtifactRepository>();
        var artifact = RuntimeArtifact(new(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Deprecated;
        artifact.IngressGeneration = 7;
        artifact.ArtifactPayload = System.Text.Json.JsonSerializer.SerializeToNode(new OrchestrationArtifact(
            Id.New(), artifact.SourceOrchestrationVersionId, artifact.OrchestrationDefinitionKey, "Sales", "sales",
            artifact.Version, artifact.ArtifactChecksum, [], [], []),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        await artifacts.Upsert(artifact);
        var instances = services.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = Instance(artifact.Id, OrchestrationInstanceStatus.DeadLettered, DateTime.UtcNow.AddDays(-2));
        await instances.Create(instance);
        Assert.NotNull(await instances.TryAcquireLease(instance.Id, "recovering", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)));
        var service = new RuntimeArtifactDeploymentService(artifacts, services.GetRequiredService<IRuntimeStorageUnitOfWork>(),
            Substitute.For<IRuntimeArtifactProjectionScheduler>(), Substitute.For<IRuntimeArtifactCompatibilityValidator>(),
            services.GetRequiredService<IRuntimeIngressConfigurationRepository>(), new DefaultRuntimeArtifactSerializer(), instances,
            services.GetRequiredService<IRuntimeArtifactDeploymentHistoryRepository>(), services.GetRequiredService<IRuntimeTriggerClaimRepository>());
        var package = new RuntimeArtifactDeliveryPackage
        {
            ArtifactId = Id.New().ToString(), ArtifactType = "orchestration-version.archived", ReleaseStrategy = "Direct",
            OrchestrationDefinitionKey = artifact.OrchestrationDefinitionKey, OrchestrationVersionId = artifact.SourceOrchestrationVersionId.ToString(),
            Version = artifact.Version.ToString(), Checksum = artifact.ArtifactChecksum.Value, PayloadJson = artifact.ArtifactPayload.ToJsonString()
        };

        var blocked = await service.DeployAsync(package, "test");
        Assert.False(blocked.Accepted);
        Assert.Equal(RuntimeOrchestrationLifecycleStatus.Deprecated, (await artifacts.GetById(artifact.Id)).LifecycleStatus);
        await instances.ReleaseLease(instance.Id, "recovering");
        var archived = await service.DeployAsync(package, "test");
        Assert.True(archived.Accepted, archived.Message);
        Assert.Equal(RuntimeOrchestrationLifecycleStatus.Archived, (await artifacts.GetById(artifact.Id)).LifecycleStatus);
    }

    [Fact]
    public async Task RecoverArchivedDeadLetterPreparesBackchannelAndResumesPersistedInstance()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifacts = services.GetRequiredService<IRuntimeArtifactRepository>();
        var artifact = RuntimeArtifact(new(1, 0, 0), RuntimeOrchestrationArtifactStatus.Retired);
        artifact.IsActive = false;
        artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived;
        artifact.IngressGeneration = 7;
        artifact.ArtifactPayload = System.Text.Json.JsonSerializer.SerializeToNode(new OrchestrationArtifact(
            Id.New(), artifact.SourceOrchestrationVersionId, artifact.OrchestrationDefinitionKey, "Sales", "sales",
            artifact.Version, artifact.ArtifactChecksum, [], [], []),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        await artifacts.Upsert(artifact);
        db.RuntimeIngressConfigurations.AddRange(
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, ConfigurationKey = "trigger",
                IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = false },
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, ConfigurationKey = "backchannel",
                IngressKind = IngressKind.Backchannel, SettingsPayload = "{}", IsActive = false });
        await db.SaveChangesAsync();
        var instances = services.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = Instance(artifact.Id, OrchestrationInstanceStatus.DeadLettered, DateTime.UtcNow.AddDays(-2));
        await instances.Create(instance);
        var activeInstances = Assert.IsAssignableFrom<IOrchestrationArtifactInstanceReader>(instances);
        Assert.False(await activeInstances.HasActiveInstances(artifact.Id));
        Assert.NotNull(await instances.TryAcquireLease(instance.Id, "recovery-test", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)));
        Assert.True(await activeInstances.HasActiveInstances(artifact.Id));
        await instances.ReleaseLease(instance.Id, "recovery-test");
        Assert.False(await activeInstances.HasActiveInstances(artifact.Id));
        var registry = Substitute.For<IIngressRegistry, IArchivedRecoveryIngressRegistry>();
        var preparation = Substitute.For<IArchivedRecoveryIngressPreparation>();
        ((IArchivedRecoveryIngressRegistry)registry).PrepareArchivedRecoveryAsync(artifact.Id.ToString(), 8, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(preparation));
        var unitOfWork = services.GetRequiredService<IRuntimeStorageUnitOfWork>();
        var preparer = new ArchivedRecoveryIngressPreparer(artifacts, unitOfWork, registry,
            Substitute.For<IRuntimeArtifactReadyNotifier>(), NullLogger<ArchivedRecoveryIngressPreparer>.Instance);
        var engine = Substitute.For<ISagaEngine>();
        var recovery = new DefaultOrchestrationRecoveryService(instances,
            services.GetRequiredService<IStageExecutionRepository>(), services.GetRequiredService<ITaskExecutionRepository>(),
            services.GetRequiredService<IExecutionTransitionRepository>(),
            new DefaultRuntimeArtifactResolver(artifacts, new DefaultRuntimeArtifactSerializer()),
            Substitute.For<ITaskAttemptDispatcher>(), engine, services.GetRequiredService<ITaskExecutionAttemptRepository>(),
            services.GetRequiredService<ITaskDispatchRepository>(), Substitute.For<ITaskDispatchReplayService>(), unitOfWork,
            Substitute.For<IOrchestrationDispatchCancellationCoordinator>(), archivedIngressPreparer: preparer);

        var result = await recovery.RecoverTerminalAsync(instance.Id.ToString());

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(OrchestrationInstanceStatus.Running, (await instances.GetById(instance.Id)).Status);
        Assert.Equal(RuntimeOrchestrationLifecycleStatus.Archived, (await artifacts.GetById(artifact.Id)).LifecycleStatus);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "backchannel")).IsActive);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "trigger")).IsActive);
        preparation.Received(1).Complete();
        await engine.Received(1).OrchestrateAsync(Arg.Is<ForwardIntent>(x => x.ArtifactId == artifact.Id.ToString()), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchivedRecoveryActivatesOnlyAfterBackchannelIsReady()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifacts = services.GetRequiredService<IRuntimeArtifactRepository>();
        var artifact = RuntimeArtifact(new(1, 0, 0), RuntimeOrchestrationArtifactStatus.Retired);
        artifact.IsActive = false;
        artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived;
        artifact.IngressGeneration = 7;
        await artifacts.Upsert(artifact);
        db.RuntimeIngressConfigurations.AddRange(
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, ConfigurationKey = "trigger",
                IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = false },
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, ConfigurationKey = "backchannel",
                IngressKind = IngressKind.Backchannel, SettingsPayload = "{}", IsActive = false });
        await db.SaveChangesAsync();
        var registry = Substitute.For<IIngressRegistry, IArchivedRecoveryIngressRegistry>();
        var archivedRegistry = (IArchivedRecoveryIngressRegistry)registry;
        archivedRegistry.PrepareArchivedRecoveryAsync(artifact.Id.ToString(), 8, Arg.Any<CancellationToken>())
            .Returns<Task<IArchivedRecoveryIngressPreparation>>(_ => throw new InvalidOperationException("Broker not ready"));
        var notifier = Substitute.For<IRuntimeArtifactReadyNotifier>();
        var preparer = new ArchivedRecoveryIngressPreparer(artifacts, services.GetRequiredService<IRuntimeStorageUnitOfWork>(),
            registry, notifier, NullLogger<ArchivedRecoveryIngressPreparer>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => preparer.PrepareAsync(artifact.Id, default));
        Assert.False((await artifacts.GetById(artifact.Id)).IsActive);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "backchannel")).IsActive);

        var preparation = Substitute.For<IArchivedRecoveryIngressPreparation>();
        archivedRegistry.PrepareArchivedRecoveryAsync(artifact.Id.ToString(), 9, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(preparation));
        await preparer.PrepareAsync(artifact.Id, default);
        preparation.Received(1).Complete();
        Assert.True((await artifacts.GetById(artifact.Id)).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "backchannel")).IsActive);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "trigger")).IsActive);
        await notifier.Received(1).NotifyReadyAsync(Arg.Is<RuntimeArtifactReadyGossipMessage>(x => x.IngressGeneration == 10), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchivedRecoveryAccessorReturnsOnlyBackchannelForMatchingRetiredGeneration()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifact = RuntimeArtifact(new(1, 0, 0), RuntimeOrchestrationArtifactStatus.Retired);
        artifact.IsActive = false;
        artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived;
        artifact.IngressGeneration = 7;
        await scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>().Upsert(artifact);
        db.RuntimeIngressConfigurations.AddRange(
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, ConfigurationKey = "trigger",
                IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = false },
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, ConfigurationKey = "backchannel",
                IngressKind = IngressKind.Backchannel, SettingsPayload = "{}", IsActive = false });
        await db.SaveChangesAsync();
        var accessor = scope.ServiceProvider.GetRequiredService<IGetArchivedBackchannelConfigurationAccessor>();

        var backchannel = Assert.Single(await accessor.GetArchivedBackchannelsAsync(artifact.Id.ToString(), 7));
        Assert.Equal(IngressKind.Backchannel, backchannel.IngressKind);
        Assert.True(backchannel.IsStaged);
        Assert.Empty(await accessor.GetArchivedBackchannelsAsync(artifact.Id.ToString(), 6));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IGetIngressConfigurationByArtifactAccessor>()
            .GetConfigurationAsync(artifact.Id.ToString()));

        using var competingScope = provider.CreateScope();
        var competingDb = competingScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await competingDb.RuntimeOrchestrationArtifacts.SingleAsync(x => x.Id == artifact.Id);
        var activation = Assert.IsAssignableFrom<IArchivedRecoveryActivationRepository>(scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>());
        var competing = Assert.IsAssignableFrom<IArchivedRecoveryActivationRepository>(competingScope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>());
        Assert.Equal(8, await activation.TryActivateArchivedBackchannelsAsync(artifact.Id, 7));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => competing.TryActivateArchivedBackchannelsAsync(artifact.Id, 7));
        var stored = await db.RuntimeOrchestrationArtifacts.AsNoTracking().SingleAsync(x => x.Id == artifact.Id);
        Assert.True(stored.IsActive);
        Assert.Equal(RuntimeOrchestrationLifecycleStatus.Archived, stored.LifecycleStatus);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, stored.Status);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>()
            .GetActive(artifact.OrchestrationDefinitionKey));
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "trigger")).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "backchannel")).IsActive);
        Assert.Equal(IngressKind.Backchannel, Assert.Single(await scope.ServiceProvider.GetRequiredService<IGetIngressConfigurationByArtifactAccessor>()
            .GetConfigurationAsync(artifact.Id.ToString())).IngressKind);
    }

    [Fact]
    public async Task ManualRollbackKeepsCallbacksAndSwitchesOnlyAfterPreparedIngress()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddKrackendOrchestrationsRuntime();
        registrations.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite(connection));
        registrations.AddSingleton(Substitute.For<IMuleCommitNotifier>());
        await using var provider = registrations.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifacts = services.GetRequiredService<IRuntimeArtifactRepository>();
        var ingress = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var claims = services.GetRequiredService<IRuntimeTriggerClaimRepository>();
        var keyBuilder = services.GetRequiredService<IRuntimeIngressConfigurationKeyBuilder>();
        var old = RuntimeArtifact(new(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var current = RuntimeArtifact(new(2, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var other = RuntimeArtifact(new(3, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var oldTrigger = new TriggerBindingArtifact(Id.New(), TriggerType.Event,
            new EventTriggerChannelArtifact(null!, "events.orders", new(1, 0, 0)), true);
        var currentTrigger = new TriggerBindingArtifact(Id.New(), TriggerType.Event,
            new EventTriggerChannelArtifact(null!, "events.orders", new(1, 0, 0)), true);
        var exclusiveTrigger = new TriggerBindingArtifact(Id.New(), TriggerType.Event,
            new EventTriggerChannelArtifact(null!, "events.orders.new", new(1, 0, 0)), true);
        var inactiveSharedTrigger = new TriggerBindingArtifact(Id.New(), TriggerType.Event,
            new EventTriggerChannelArtifact(null!, "events.orders", new(1, 0, 0)), true);
        var otherTrigger = new TriggerBindingArtifact(Id.New(), TriggerType.Event,
            new EventTriggerChannelArtifact(null!, "events.orders.other", new(1, 0, 0)), true);
        foreach (var pair in new[] { (old, oldTrigger), (current, currentTrigger) })
        {
            pair.Item1.ArtifactPayload = System.Text.Json.JsonSerializer.SerializeToNode(new OrchestrationArtifact(
                Id.New(), pair.Item1.SourceOrchestrationVersionId, pair.Item1.OrchestrationDefinitionKey, "Orders", "sales",
                pair.Item1.Version, pair.Item1.ArtifactChecksum,
                pair.Item1.Id == current.Id ? [pair.Item2, exclusiveTrigger] : [pair.Item2], [], []),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
            await artifacts.Upsert(pair.Item1);
        }
        other.ArtifactPayload = System.Text.Json.JsonSerializer.SerializeToNode(new OrchestrationArtifact(
            Id.New(), other.SourceOrchestrationVersionId, other.OrchestrationDefinitionKey, "Orders", "sales",
            other.Version, other.ArtifactChecksum, [inactiveSharedTrigger, otherTrigger], [], []),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        await artifacts.Upsert(other);
        var oldKey = keyBuilder.BuildTriggerKey(oldTrigger);
        var currentKey = keyBuilder.BuildTriggerKey(currentTrigger);
        var exclusiveKey = keyBuilder.BuildTriggerKey(exclusiveTrigger);
        var otherKey = keyBuilder.BuildTriggerKey(otherTrigger);
        db.RuntimeIngressConfigurations.AddRange(
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = old.Id, ConfigurationKey = oldKey,
                IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = false },
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = old.Id, ConfigurationKey = "backchannel:old",
                IngressKind = IngressKind.Backchannel, SettingsPayload = "{}", IsActive = true },
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = current.Id, ConfigurationKey = currentKey,
                IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = true },
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = current.Id, ConfigurationKey = exclusiveKey,
                IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = true },
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = current.Id, ConfigurationKey = "backchannel:current",
                IngressKind = IngressKind.Backchannel, SettingsPayload = "{}", IsActive = true },
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = other.Id, ConfigurationKey = keyBuilder.BuildTriggerKey(inactiveSharedTrigger),
                IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = false },
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = other.Id, ConfigurationKey = otherKey,
                IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = true });
        await db.SaveChangesAsync();
        const string identity = "MESSAGING|EVENTS.ORDERS|1.0.0";
        const string exclusiveIdentity = "MESSAGING|EVENTS.ORDERS.NEW|1.0.0";
        Assert.True((await claims.ClaimAsync([identity, exclusiveIdentity], current.Id, [])).Succeeded);

        var result = await services.GetRequiredService<IRuntimeArtifactRollbackService>().RollbackAsync(old.Id.ToString(), "operator");
        Assert.True(result.Accepted, result.Message);
        var durable = await db.Set<DurableAction>().SingleAsync();
        var request = System.Text.Json.JsonSerializer.Deserialize<RuntimeArtifactProjectionRequest>(durable.Payload,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Equal(oldKey, Assert.Single(request.RollbackTargetTriggerKeys));
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Pending, (await db.RuntimeArtifactDeploymentHistory.AsNoTracking().SingleAsync()).RuntimeStatus);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == oldKey)).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == currentKey)).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == exclusiveKey)).IsActive);

        var registry = Substitute.For<IIngressRegistry, IRollbackIngressRegistry>();
        var preparation = Substitute.For<IRollbackIngressPreparation>();
        ((IRollbackIngressRegistry)registry).PrepareRollbackAsync(old.Id.ToString(), request.IngressGeneration,
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns<Task<IRollbackIngressPreparation>>(_ => throw new InvalidOperationException("Consumer is unavailable"));
        var notifier = Substitute.For<IRuntimeArtifactReadyNotifier>();
        notifier.NotifyReadyAsync(Arg.Any<RuntimeArtifactReadyGossipMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Gossip unavailable")), Task.CompletedTask);
        var action = new ProjectIngressConfigurationsAction(artifacts, Substitute.For<IRuntimeIngressConfigurationProjector>(),
            services.GetRequiredService<IRuntimeStorageUnitOfWork>(), notifier, NullLogger<ProjectIngressConfigurationsAction>.Instance,
            new DefaultMuleTerminalFailureMarker(), registry, ingress, claims,
            services.GetRequiredService<IRuntimeArtifactDeploymentHistoryRepository>());
        var context = new MuleActionContext<RuntimeArtifactProjectionRequest>(new DurableAction
        {
            Key = ActionKey.From(RuntimeArtifactActionNames.ProjectIngressConfigurations), Lane = RuntimeArtifactProjectionSchedulerDefaults.Lane,
            CorrelationId = old.Id.ToString(), DeduplicationKey = old.Id.ToString(), Status = DurableActionStatus.Pending
        }, services, request);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await action.ExecuteAsync(context, default));
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Failed, (await db.RuntimeArtifactDeploymentHistory.AsNoTracking().SingleAsync()).RuntimeStatus);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == oldKey)).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == currentKey)).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == exclusiveKey)).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "backchannel:old")).IsActive);
        ((IRollbackIngressRegistry)registry).PrepareRollbackAsync(old.Id.ToString(), request.IngressGeneration,
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(preparation));
        await Assert.ThrowsAsync<IOException>(async () => await action.ExecuteAsync(context, default));

        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == oldKey)).IsActive);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == currentKey)).IsActive);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == exclusiveKey)).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "backchannel:current")).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == otherKey)).IsActive);
        Assert.True((await claims.ClaimAsync([exclusiveIdentity], Id.New(), [])).Succeeded);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "backchannel:old")).IsActive);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, (await artifacts.GetById(old.Id)).Status);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, (await db.RuntimeArtifactDeploymentHistory.AsNoTracking().SingleAsync()).RuntimeStatus);
        preparation.Received(1).Complete();
        Assert.Equal(request.IngressGeneration + 1, (await artifacts.GetById(old.Id)).IngressGeneration);
        // A durable retry after commit must republish the refresh without changing the successful outcome.
        await action.ExecuteAsync(context, default);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, (await db.RuntimeArtifactDeploymentHistory.AsNoTracking().SingleAsync()).RuntimeStatus);
        await notifier.Received(2).NotifyReadyAsync(Arg.Is<RuntimeArtifactReadyGossipMessage>(x =>
            x.IngressGeneration == request.IngressGeneration + 1 && x.RefreshToken == request.RollbackOperationId), Arg.Any<CancellationToken>());
        await notifier.Received(1).NotifyReadyAsync(Arg.Is<RuntimeArtifactReadyGossipMessage>(x =>
            x.ArtifactId == current.Id.ToString() && x.RefreshToken == request.RollbackOperationId), Arg.Any<CancellationToken>());

        const string staleOperation = "runtime-rollback:stale";
        await services.GetRequiredService<IRuntimeArtifactDeploymentHistoryRepository>().Append(new RuntimeArtifactDeploymentRecord
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = old.Id, OrchestrationDefinitionKey = old.OrchestrationDefinitionKey,
            OrchestrationVersion = old.Version.ToString(), ControlPlaneArtifactId = staleOperation,
            ArtifactType = "orchestration-version.rollback-requested", ReleaseStrategy = "Rollback",
            RuntimeStatus = RuntimeOrchestrationArtifactStatus.Pending, AppliedOnUtc = DateTime.UtcNow
        });
        var staleRequest = new RuntimeArtifactProjectionRequest
        {
            ArtifactId = old.Id.ToString(), IngressGeneration = request.IngressGeneration - 1,
            RollbackTargetTriggerKeys = [oldKey], RollbackOperationId = staleOperation
        };
        var staleContext = new MuleActionContext<RuntimeArtifactProjectionRequest>(new DurableAction
        {
            Key = ActionKey.From(RuntimeArtifactActionNames.ProjectIngressConfigurations), Lane = RuntimeArtifactProjectionSchedulerDefaults.Lane,
            CorrelationId = old.Id.ToString(), DeduplicationKey = "stale", Status = DurableActionStatus.Pending
        }, services, staleRequest);
        await action.ExecuteAsync(staleContext, default);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Failed, (await db.RuntimeArtifactDeploymentHistory.AsNoTracking()
            .SingleAsync(x => x.ControlPlaneArtifactId == staleOperation)).RuntimeStatus);
    }

    [Fact]
    public async Task LateRollbackFailureCannotOverwriteSuccessfulOutcome()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var failingScope = provider.CreateScope();
        using var successScope = provider.CreateScope();
        var failingDb = failingScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var successDb = successScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await failingDb.Database.EnsureCreatedAsync();
        var record = new RuntimeArtifactDeploymentRecord
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), OrchestrationDefinitionKey = "orders",
            OrchestrationVersion = "1.0.0", ControlPlaneArtifactId = "runtime-rollback:test", ReleaseId = "runtime-rollback:test",
            ArtifactType = "orchestration-version.rollback-requested", ReleaseStrategy = "Rollback",
            RuntimeStatus = RuntimeOrchestrationArtifactStatus.Pending, AppliedOnUtc = DateTime.UtcNow
        };
        failingDb.RuntimeArtifactDeploymentHistory.Add(record);
        await failingDb.SaveChangesAsync();
        var successful = (IRuntimeArtifactRollbackStatusRepository)successScope.ServiceProvider.GetRequiredService<IRuntimeArtifactDeploymentHistoryRepository>();
        var failing = (IRuntimeArtifactRollbackStatusRepository)failingScope.ServiceProvider.GetRequiredService<IRuntimeArtifactDeploymentHistoryRepository>();
        Assert.True(await successful.TrySetRollbackStatus(record.ControlPlaneArtifactId, RuntimeOrchestrationArtifactStatus.Ready));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => failing.TrySetRollbackStatus(record.ControlPlaneArtifactId, RuntimeOrchestrationArtifactStatus.Failed));
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, (await successDb.RuntimeArtifactDeploymentHistory.AsNoTracking().SingleAsync()).RuntimeStatus);
    }

    [Fact]
    public async Task LifetimeSupervisorQueriesCandidatesOnceForManyVersionsWithoutInstances()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var commands = new List<string>();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted]));
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await context.Database.EnsureCreatedAsync();
        for (var i = 1; i <= 20; i++)
        {
            var artifact = RuntimeArtifact(new SemanticVersion(i, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
            artifact.ArtifactPayload = System.Text.Json.JsonSerializer.SerializeToNode(new OrchestrationArtifact(
                Id.New(), Id.New(), artifact.OrchestrationDefinitionKey, "Sales", "sales", artifact.Version,
                artifact.ArtifactChecksum, [], [], []))!;
            context.RuntimeOrchestrationArtifacts.Add(artifact);
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        commands.Clear();

        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<OrchestrationLifetimeProcessor>().ProcessAsync(DateTime.UtcNow));

        Assert.Single(commands.Where(x => x.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
            x.Contains("FROM \"OrchestrationInstances\"", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(commands, x => x.Contains("FROM \"RuntimeOrchestrationArtifacts\"", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LateConnectorFailureCannotOverwriteAnotherReplicasSuccessfulActivation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var failingScope = provider.CreateScope();
        using var successfulScope = provider.CreateScope();
        var failingDb = failingScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var successfulDb = successfulScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await failingDb.Database.EnsureCreatedAsync();
        var artifact = RuntimeArtifact(new(1, 0, 0), RuntimeOrchestrationArtifactStatus.Pending);
        failingDb.RuntimeOrchestrationArtifacts.Add(artifact);
        await failingDb.SaveChangesAsync();
        await successfulDb.RuntimeOrchestrationArtifacts.SingleAsync(x => x.Id == artifact.Id);
        var failed = (IRuntimeArtifactRolloutTransitionRepository)failingScope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        var ready = (IRuntimeArtifactRolloutTransitionRepository)successfulScope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        Assert.True(await ready.TryMarkRolloutReady(artifact.Id, artifact.IngressGeneration));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            failed.TryMarkRolloutStandupFailed(artifact.Id, artifact.IngressGeneration, "late failure"));
        using var checkScope = provider.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready,
            (await check.RuntimeOrchestrationArtifacts.AsNoTracking().SingleAsync(x => x.Id == artifact.Id)).Status);
    }

    [Fact]
    public async Task PendingRolloutCanBePreparedButIsNotRoutable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifacts = services.GetRequiredService<IRuntimeArtifactRepository>();
        var ingress = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var old = RuntimeArtifact(new(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var next = RuntimeArtifact(new(2, 0, 0), RuntimeOrchestrationArtifactStatus.Pending);
        await artifacts.Upsert(old);
        await artifacts.Upsert(next);
        foreach (var artifact in new[] { old, next })
            await ingress.UpsertForArtifactAsync(artifact.Id, [new RuntimeIngressConfiguration
            {
                Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, ConfigurationKey = "trigger:one",
                IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = true
            }]);

        var staged = services.GetRequiredService<IGetStagedIngressConfigurationAccessor>();
        Assert.True(Assert.Single(await staged.GetStagedConfigurationAsync(next.Id.ToString(), next.IngressGeneration)).IsStaged);
        Assert.Empty(await staged.GetStagedConfigurationAsync(next.Id.ToString(), next.IngressGeneration + 1));
        var ready = services.GetRequiredService<IGetIngressConfigurationByArtifactAccessor>();
        Assert.Empty(await ready.GetConfigurationAsync(next.Id.ToString()));
        using var all = services.GetRequiredService<IGetAllIngressConfigurationsAccessor>();
        Assert.Equal(old.Id.ToString(), Assert.Single((await all.ReadAsync()).Configurations).ArtifactId);
    }

    [Fact]
    public async Task FailedConnectorKeepsOldTriggerAndRetryActivatesNewVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifacts = services.GetRequiredService<IRuntimeArtifactRepository>();
        var ingress = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var claims = services.GetRequiredService<IRuntimeTriggerClaimRepository>();
        var unitOfWork = services.GetRequiredService<IRuntimeStorageUnitOfWork>();
        var old = RuntimeArtifact(new(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var next = RuntimeArtifact(new(2, 0, 0), RuntimeOrchestrationArtifactStatus.Pending);
        await artifacts.Upsert(old);
        await artifacts.Upsert(next);
        const string identity = "MESSAGING|ORDERS|1.0.0";
        const string key = "trigger:old";
        await ingress.UpsertForArtifactAsync(old.Id, [new RuntimeIngressConfiguration
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = old.Id, ConfigurationKey = key,
            IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = true
        }]);
        Assert.True((await claims.ClaimAsync([identity], next.Id, [])).Succeeded);
        var projector = Substitute.For<IRuntimeIngressConfigurationProjector>();
        var registry = Substitute.For<IIngressRegistry, IStagedIngressRegistry>();
        var staged = (IStagedIngressRegistry)registry;
        staged.StandUpStagedAsync(next.Id.ToString(), next.IngressGeneration, Arg.Any<CancellationToken>())
            .Returns(_ => throw new IngressStandupConfigurationException("connector unavailable"));
        var notifier = Substitute.For<IRuntimeArtifactReadyNotifier>();
        var action = new ProjectIngressConfigurationsAction(artifacts, projector, unitOfWork, notifier,
            NullLogger<ProjectIngressConfigurationsAction>.Instance, new DefaultMuleTerminalFailureMarker(), registry, ingress, claims);
        var request = new RuntimeArtifactProjectionRequest
        {
            ArtifactId = next.Id.ToString(), IngressGeneration = next.IngressGeneration,
            RolloutTriggerIdentities = [identity],
            RolloutReplacements = [new() { ArtifactId = old.Id.ToString(), ConfigurationKeys = [key] }]
        };
        var context = new MuleActionContext<RuntimeArtifactProjectionRequest>(new DurableAction
        {
            Key = ActionKey.From(RuntimeArtifactActionNames.ProjectIngressConfigurations),
            Lane = RuntimeArtifactProjectionSchedulerDefaults.Lane, CorrelationId = next.Id.ToString(),
            DeduplicationKey = next.Id.ToString(), Status = DurableActionStatus.Pending
        }, services, request);

        await Assert.ThrowsAsync<IngressStandupConfigurationException>(async () => await action.ExecuteAsync(context, default));
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync()).IsActive);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Failed, (await artifacts.GetById(next.Id)).Status);
        await notifier.DidNotReceiveWithAnyArgs().NotifyReadyAsync(default!, default);
        await registry.Received(1).ShutDownOneAsync(next.Id.ToString(), Arg.Any<CancellationToken>());

        staged.StandUpStagedAsync(next.Id.ToString(), next.IngressGeneration, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await action.ExecuteAsync(context, default);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync()).IsActive);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, (await artifacts.GetById(next.Id)).Status);
        await notifier.Received(1).NotifyReadyAsync(Arg.Any<RuntimeArtifactReadyGossipMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RolledBackPendingTriggerChangesAreNotSavedWithTheSubsequentError()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var ingress = scope.ServiceProvider.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>();
        var oldId = Id.New();
        await ingress.UpsertForArtifactAsync(oldId, [new RuntimeIngressConfiguration
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = oldId, ConfigurationKey = "trigger:old",
            IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = true
        }]);

        await using (await unitOfWork.BeginTransactionAsync())
        {
            using (unitOfWork.DeferAutoSave())
                await ((IRuntimeSelectiveTriggerIngressRepository)ingress).DeactivateTriggersAsync(oldId, ["trigger:old"]);
        }
        await unitOfWork.SaveChanges();

        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync()).IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RolloutHandoffUsesTheExistingEfTransaction(bool commit)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        var oldId = Id.New();
        var newId = Id.New();
        const string identity = "MESSAGING|ORDERS|1.0.0";
        const string key = "trigger:old";
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
            await db.Database.EnsureCreatedAsync();
            var ingress = scope.ServiceProvider.GetRequiredService<IRuntimeIngressConfigurationRepository>();
            var claims = scope.ServiceProvider.GetRequiredService<IRuntimeTriggerClaimRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>();
            await ingress.UpsertForArtifactAsync(oldId, [new RuntimeIngressConfiguration
            {
                Id = Id.New(), RuntimeOrchestrationArtifactId = oldId, ConfigurationKey = key,
                IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = true
            }]);
            Assert.True((await claims.ClaimAsync([identity], newId, [])).Succeeded);
            await using var transaction = await unitOfWork.BeginTransactionAsync();
            using (unitOfWork.DeferAutoSave())
            {
                await RuntimeArtifactRolloutHandoff.ApplyAsync(new RuntimeArtifactProjectionRequest
                {
                    ArtifactId = newId.ToString(), RolloutTriggerIdentities = [identity],
                    RolloutReplacements = [new() { ArtifactId = oldId.ToString(), ConfigurationKeys = [key] }]
                }, ingress, claims);
                await unitOfWork.SaveChanges();
            }
            if (commit) await transaction.CommitAsync();
        }
        using var verificationScope = provider.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        Assert.Equal(!commit, (await verification.RuntimeIngressConfigurations.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task ConcurrentRolloutsCannotBothReplaceTheSameTriggerOwner()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await firstDb.Database.EnsureCreatedAsync();

        var previousOwner = Id.New();
        var firstOwner = Id.New();
        var secondOwner = Id.New();
        const string identity = "MESSAGING|ORDERS|1.0.0";
        firstDb.RuntimeTriggerClaims.Add(new RuntimeTriggerClaim
        {
            Id = identity, RuntimeOrchestrationArtifactId = previousOwner, UpdatedOnUtc = DateTime.UtcNow
        });
        await firstDb.SaveChangesAsync();
        await secondDb.RuntimeTriggerClaims.SingleAsync(x => x.Id == identity);

        var first = firstScope.ServiceProvider.GetRequiredService<IRuntimeTriggerClaimRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<IRuntimeTriggerClaimRepository>();
        Assert.True((await first.ClaimAsync([identity], firstOwner, [previousOwner])).Succeeded);
        Assert.False((await second.ClaimAsync([identity], secondOwner, [previousOwner])).Succeeded);
        Assert.Equal(firstOwner, (await secondDb.RuntimeTriggerClaims.AsNoTracking().SingleAsync(x => x.Id == identity)).RuntimeOrchestrationArtifactId);
        Assert.DoesNotContain(secondDb.ChangeTracker.Entries<RuntimeTriggerClaim>(), x => x.State == EntityState.Modified);
    }

    [Fact]
    public async Task ExpirationAndIdleDeadLetterUnblockArchiveOnlyAfterAllInstancesFinish()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var setup = provider.CreateScope();
        var setupServices = setup.ServiceProvider;
        await setupServices.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var artifact = RuntimeArtifact(new(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        artifact.LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Deprecated;
        artifact.ArtifactPayload = System.Text.Json.JsonSerializer.SerializeToNode(new OrchestrationArtifact(
            Id.New(), artifact.SourceOrchestrationVersionId, artifact.OrchestrationDefinitionKey, "Sales", "sales",
            artifact.Version, artifact.ArtifactChecksum, [], [], []),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        await setupServices.GetRequiredService<IRuntimeArtifactRepository>().Upsert(artifact);
        var instances = setupServices.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;
        var old = Instance(artifact.Id, OrchestrationInstanceStatus.Running, now.AddDays(-2));
        var recent = Instance(artifact.Id, OrchestrationInstanceStatus.Running, now);
        await instances.Create(old);
        await instances.Create(recent);
        Assert.True(await setupServices.GetRequiredService<OrchestrationLifetimeBackfill>().ProcessAsync());
        var package = new RuntimeArtifactDeliveryPackage
        {
            ArtifactId = Id.New().ToString(), ArtifactType = "orchestration-version.archived", ReleaseStrategy = "Direct",
            OrchestrationDefinitionKey = artifact.OrchestrationDefinitionKey, OrchestrationVersionId = artifact.SourceOrchestrationVersionId.ToString(),
            Version = artifact.Version.ToString(), Checksum = artifact.ArtifactChecksum.Value, PayloadJson = artifact.ArtifactPayload.ToJsonString()
        };

        async Task<RuntimeArtifactDeploymentResult> ArchiveAsync()
        {
            using var scope = provider.CreateScope();
            var services = scope.ServiceProvider;
            var service = new RuntimeArtifactDeploymentService(services.GetRequiredService<IRuntimeArtifactRepository>(),
                services.GetRequiredService<IRuntimeStorageUnitOfWork>(), Substitute.For<IRuntimeArtifactProjectionScheduler>(),
                Substitute.For<IRuntimeArtifactCompatibilityValidator>(), services.GetRequiredService<IRuntimeIngressConfigurationRepository>(),
                new DefaultRuntimeArtifactSerializer(), services.GetRequiredService<IOrchestrationInstanceRepository>(),
                services.GetRequiredService<IRuntimeArtifactDeploymentHistoryRepository>(), services.GetRequiredService<IRuntimeTriggerClaimRepository>());
            return await service.DeployAsync(package, "test");
        }

        Assert.False((await ArchiveAsync()).Accepted);
        using (var scope = provider.CreateScope())
        {
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OrchestrationLifetimeProcessor>().ProcessAsync(now));
            var stored = await scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>().GetById(old.Id);
            Assert.Equal(OrchestrationInstanceStatus.Idle, stored.Status);
            Assert.InRange(stored.IdleExpiresOnUtc!.Value, now.AddMinutes(20), DateTime.UtcNow.AddMinutes(20).AddSeconds(1));
        }
        Assert.False((await ArchiveAsync()).Accepted);
        using (var scope = provider.CreateScope())
        {
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IOrchestrationIdleProcessor>().ProcessExpiredAsync(now.AddHours(1)));
            var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
            Assert.Equal(OrchestrationInstanceStatus.DeadLettered, (await repository.GetById(old.Id)).Status);
            Assert.Equal(OrchestrationInstanceStatus.Running, (await repository.GetById(recent.Id)).Status);
        }
        Assert.False((await ArchiveAsync()).Accepted);
        using (var scope = provider.CreateScope())
        {
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OrchestrationLifetimeProcessor>().ProcessAsync(now.AddDays(2)));
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IOrchestrationIdleProcessor>().ProcessExpiredAsync(now.AddHours(1)));
        }
        var result = await ArchiveAsync();
        Assert.True(result.Accepted, result.Message);
        using var verification = provider.CreateScope();
        Assert.Equal(OrchestrationInstanceStatus.DeadLettered,
            (await verification.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>().GetById(recent.Id)).Status);
        Assert.Equal(RuntimeOrchestrationLifecycleStatus.Archived,
            (await verification.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>().GetById(artifact.Id)).LifecycleStatus);
    }

    [Fact]
    public async Task ExpirationSupervisorReadsOnlyExpiredProjectedInstancesWithoutArtifacts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var commands = new List<string>();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted]));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var expired = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddDays(-2));
        expired.MaxInstanceDurationSeconds = 86400;
        expired.IdleDurationSeconds = 300;
        expired.ExpiresOnUtc = now.AddHours(-1);
        var future = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddDays(-2));
        future.MaxInstanceDurationSeconds = 604800;
        future.IdleDurationSeconds = 1200;
        future.ExpiresOnUtc = now.AddDays(5);
        var disabled = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddDays(-2));
        disabled.MaxInstanceDurationSeconds = 0;
        disabled.IdleDurationSeconds = 1200;
        var instances = services.GetRequiredService<IOrchestrationInstanceRepository>();
        await instances.Create(expired);
        await instances.Create(future);
        await instances.Create(disabled);
        db.ChangeTracker.Clear();
        commands.Clear();

        Assert.Equal(1, await services.GetRequiredService<OrchestrationLifetimeProcessor>().ProcessAsync(now));

        Assert.Equal(OrchestrationInstanceStatus.Idle, (await instances.GetById(expired.Id)).Status);
        Assert.Equal(OrchestrationInstanceStatus.Running, (await instances.GetById(future.Id)).Status);
        Assert.Equal(OrchestrationInstanceStatus.Running, (await instances.GetById(disabled.Id)).Status);
        Assert.DoesNotContain(commands, x => x.Contains("FROM \"RuntimeOrchestrationArtifacts\"", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands, x => x.Contains("ExpiresOnUtc", StringComparison.OrdinalIgnoreCase) &&
            x.Contains("OrchestrationInstances", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LifetimeSupervisorMovesAnExistingOverdueSagaToIdleUsingEfStorage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var artifact = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        artifact.ArtifactPayload = System.Text.Json.JsonSerializer.SerializeToNode(new OrchestrationArtifact(
            Id.New(), Id.New(), artifact.OrchestrationDefinitionKey, "Sales", "sales", artifact.Version,
            artifact.ArtifactChecksum, [], [], []) { IdleDurationSeconds = 3600 })!;
        await services.GetRequiredService<IRuntimeArtifactRepository>().Upsert(artifact);
        var instances = services.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;
        var old = Instance(artifact.Id, OrchestrationInstanceStatus.Running, now.AddDays(-2));
        await instances.Create(old);

        Assert.True(await services.GetRequiredService<OrchestrationLifetimeBackfill>().ProcessAsync());

        var changed = await services.GetRequiredService<OrchestrationLifetimeProcessor>().ProcessAsync(now);

        Assert.Equal(1, changed);
        var saved = await instances.GetById(old.Id);
        Assert.Equal(OrchestrationInstanceStatus.Idle, saved.Status);
        Assert.InRange(saved.IdleExpiresOnUtc!.Value, now.AddMinutes(59), DateTime.UtcNow.AddHours(1).AddSeconds(1));

        var deadLettered = await services.GetRequiredService<IOrchestrationIdleProcessor>().ProcessExpiredAsync(now.AddHours(2));
        Assert.Equal(1, deadLettered);
        Assert.Equal(OrchestrationInstanceStatus.DeadLettered, (await instances.GetById(old.Id)).Status);
    }

    [Fact]
    public async Task LifetimeSupervisorProcessesMoreThanOneEfPage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<RuntimeDbContext>();
        await context.Database.EnsureCreatedAsync();
        var artifact = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        artifact.ArtifactPayload = System.Text.Json.JsonSerializer.SerializeToNode(new OrchestrationArtifact(
            Id.New(), Id.New(), artifact.OrchestrationDefinitionKey, "Sales", "sales", artifact.Version,
            artifact.ArtifactChecksum, [], [], []))!;
        await services.GetRequiredService<IRuntimeArtifactRepository>().Upsert(artifact);
        var now = DateTime.UtcNow;
        context.OrchestrationInstances.AddRange(Enumerable.Range(0, 125)
            .Select(i => Instance(artifact.Id, OrchestrationInstanceStatus.Running, now.AddDays(-2).AddSeconds(i))));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.True(await services.GetRequiredService<OrchestrationLifetimeBackfill>().ProcessAsync());

        var changed = await services.GetRequiredService<OrchestrationLifetimeProcessor>().ProcessAsync(now);

        Assert.Equal(125, changed);
        Assert.Equal(125, await context.OrchestrationInstances.CountAsync(x => x.Status == OrchestrationInstanceStatus.Idle));
    }

    [Fact]
    public async Task LifetimeCandidatesAreFilteredByArtifactStatusAndStartTime()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var candidates = Assert.IsAssignableFrom<IOrchestrationLifetimeCandidateRepository>(repository);
        var now = DateTime.UtcNow;
        var artifactId = Id.New();
        var old = Instance(artifactId, OrchestrationInstanceStatus.Running, now.AddDays(-3));
        var recent = Instance(artifactId, OrchestrationInstanceStatus.Running, now.AddHours(-1));
        var other = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddDays(-4));
        var idle = Instance(artifactId, OrchestrationInstanceStatus.Idle, now.AddDays(-4));
        foreach (var instance in new[] { old, recent, other, idle }) await repository.Create(instance);

        var found = await candidates.GetLifetimeCandidates(artifactId, now.AddDays(-1), 0, 100);

        Assert.Equal(old.Id, Assert.Single(found));
        var archiveReader = Assert.IsAssignableFrom<IOrchestrationArtifactInstanceReader>(repository);
        Assert.True(await archiveReader.HasActiveInstances(artifactId));
        old.Status = OrchestrationInstanceStatus.Completed;
        recent.Status = OrchestrationInstanceStatus.Completed;
        idle.Status = OrchestrationInstanceStatus.DeadLettered;
        await repository.Update(old); await repository.Update(recent); await repository.Update(idle);
        Assert.False(await archiveReader.HasActiveInstances(artifactId));
    }

    [Fact]
    public async Task StatisticsReportUsesProjectedStatesAndSeparatesObservedTransitions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, now);
        await repository.Create(instance);
        instance.Status = OrchestrationInstanceStatus.DeadLettered;
        await repository.Update(instance);
        instance.Status = OrchestrationInstanceStatus.Completed;
        await repository.Update(instance);
        await scope.ServiceProvider.GetRequiredService<ISagaStatisticsBatchProcessor>().ProcessAsync(10);
        var report = await scope.ServiceProvider.GetRequiredService<ISagaStatisticsReader>()
            .GetReportAsync(now.AddDays(-1), now.AddDays(1), instance.OrchestrationDefinitionKey);
        Assert.Equal(1, report.Total);
        Assert.Equal(1, report.CurrentStatuses[OrchestrationInstanceStatus.Completed]);
        Assert.False(report.CurrentStatuses.ContainsKey(OrchestrationInstanceStatus.DeadLettered));
        Assert.Equal(1, report.ReachedStatuses[OrchestrationInstanceStatus.DeadLettered]);
        Assert.Equal(1, report.ReachedStatuses[OrchestrationInstanceStatus.Completed]);
        Assert.Contains(report.Activity, item => item.Status == OrchestrationInstanceStatus.DeadLettered && item.Count == 1);
        Assert.Equal(0, report.PendingObservations);
    }

    [Fact]
    public async Task DashboardStatisticsReaderUsesExclusiveCohortStatesAndUtcBuckets()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;
        var completed = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddMinutes(-30));
        var running = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddMinutes(-20));
        await repository.Create(completed);
        await repository.Create(running);
        completed.Status = OrchestrationInstanceStatus.DeadLettered;
        await repository.Update(completed);
        completed.Status = OrchestrationInstanceStatus.Completed;
        await repository.Update(completed);
        await scope.ServiceProvider.GetRequiredService<ISagaStatisticsBatchProcessor>().ProcessAsync(20);

        var reader = scope.ServiceProvider.GetRequiredService<ISagaStatisticsDashboardReader>();
        var query = new SagaStatisticsDashboardQuery(now.AddHours(-1), now.AddHours(1), completed.OrchestrationDefinitionKey);
        var cohort = await reader.GetCohortAsync(query);
        var minute = await reader.GetActivityAsync(query, SagaStatisticsActivityBucket.Minute);
        var hourly = await reader.GetActivityAsync(query, SagaStatisticsActivityBucket.Hour);
        var daily = await reader.GetActivityAsync(new SagaStatisticsDashboardQuery(now.AddHours(-1), now.AddHours(1)),
            SagaStatisticsActivityBucket.Day);
        var weekly = await reader.GetActivityAsync(new SagaStatisticsDashboardQuery(now.AddHours(-1), now.AddHours(1)),
            SagaStatisticsActivityBucket.Week);
        var health = await reader.GetHealthAsync();

        Assert.Equal(2, cohort.Total);
        Assert.Equal(1, cohort.CurrentStatuses[OrchestrationInstanceStatus.Completed]);
        Assert.Equal(1, cohort.CurrentStatuses[OrchestrationInstanceStatus.Running]);
        Assert.DoesNotContain(cohort.CurrentStatuses, item => item.Key == OrchestrationInstanceStatus.DeadLettered);
        Assert.Contains(minute, item => item.Status == OrchestrationInstanceStatus.Completed);
        Assert.All(minute, item => Assert.Equal(0, item.BucketUtc.Second));
        Assert.All(minute, item => Assert.Equal(0, item.BucketUtc.Millisecond));
        Assert.Contains(hourly, item => item.Status == OrchestrationInstanceStatus.Completed);
        Assert.All(hourly, item => Assert.Equal(0, item.BucketUtc.Minute));
        Assert.All(daily, item => Assert.Equal(TimeSpan.Zero, item.BucketUtc.TimeOfDay));
        Assert.All(weekly, item => Assert.Equal(DayOfWeek.Monday, item.BucketUtc.DayOfWeek));
        Assert.Equal(0, health.PendingObservations);
        Assert.False(health.BackfillCompleted);
        Assert.Contains(completed.OrchestrationDefinitionKey, await reader.GetOrchestrationKeysAsync());
        Assert.Equal(2, (await reader.GetCohortAsync(new SagaStatisticsDashboardQuery(now.AddHours(-1), now.AddHours(1)))).Total);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.GetActivityAsync(query, (SagaStatisticsActivityBucket)99));
        await Assert.ThrowsAsync<ArgumentNullException>(() => reader.GetActivityAsync(null!, SagaStatisticsActivityBucket.Day));
    }

    [Fact]
    public async Task DashboardActivityCanExcludeTransitionsFromOlderInstances()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;
        var older = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddHours(-2));
        var recent = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddMinutes(-30));
        var atStart = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddHours(-1));
        var atEnd = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddHours(1));
        await repository.Create(older); await repository.Create(recent); await repository.Create(atStart); await repository.Create(atEnd);
        older.Status = OrchestrationInstanceStatus.DeadLettered; older.LastUpdatedOnUtc = now; await repository.Update(older);
        recent.Status = OrchestrationInstanceStatus.DeadLettered; recent.LastUpdatedOnUtc = now; await repository.Update(recent);
        atStart.Status = OrchestrationInstanceStatus.DeadLettered; atStart.LastUpdatedOnUtc = now; await repository.Update(atStart);
        atEnd.Status = OrchestrationInstanceStatus.DeadLettered; atEnd.LastUpdatedOnUtc = now; await repository.Update(atEnd);
        await scope.ServiceProvider.GetRequiredService<ISagaStatisticsBatchProcessor>().ProcessAsync(20);
        var reader = scope.ServiceProvider.GetRequiredService<ISagaStatisticsDashboardReader>();
        var query = new SagaStatisticsDashboardQuery(now.AddHours(-1), now.AddHours(1));
        var all = await reader.GetActivityAsync(query, SagaStatisticsActivityBucket.Hour);
        var cohort = await reader.GetActivityAsync(query with { RestrictActivityToStartedCohort = true }, SagaStatisticsActivityBucket.Hour);
        Assert.Equal(4, all.Where(item => item.Status == OrchestrationInstanceStatus.DeadLettered).Sum(item => item.Count));
        Assert.Equal(2, cohort.Where(item => item.Status == OrchestrationInstanceStatus.DeadLettered).Sum(item => item.Count));
    }

    [Fact]
    public async Task DashboardStatisticsReaderRanksStartedOrchestrationsInTheProjection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;

        var inventory = Instance(Id.New(), OrchestrationInstanceStatus.Completed, now);
        inventory.OrchestrationDefinitionKey = "inventory.stock.updated";
        var salesFirst = Instance(Id.New(), OrchestrationInstanceStatus.Running, now);
        salesFirst.OrchestrationDefinitionKey = "sales.sale.created";
        var salesSecond = Instance(Id.New(), OrchestrationInstanceStatus.Completed, now);
        salesSecond.OrchestrationDefinitionKey = "sales.sale.created";
        var outsideRange = Instance(Id.New(), OrchestrationInstanceStatus.Completed, now.AddDays(-2));
        outsideRange.OrchestrationDefinitionKey = "outside.range";
        await repository.Create(inventory);
        await repository.Create(salesFirst);
        await repository.Create(salesSecond);
        await repository.Create(outsideRange);
        await scope.ServiceProvider.GetRequiredService<ISagaStatisticsBatchProcessor>().ProcessAsync(20);

        var reader = scope.ServiceProvider.GetRequiredService<ISagaStatisticsDashboardReader>();
        var query = new SagaStatisticsDashboardQuery(now.AddHours(-1), now.AddHours(1));
        var ranking = await reader.GetTopOrchestrationsAsync(query, 10);

        Assert.Collection(ranking,
            item => Assert.Equal(new SagaStatisticsDashboardOrchestration("sales.sale.created", 2), item),
            item => Assert.Equal(new SagaStatisticsDashboardOrchestration("inventory.stock.updated", 1), item));
        Assert.Equal([new SagaStatisticsDashboardOrchestration("sales.sale.created", 2)],
            await reader.GetTopOrchestrationsAsync(query, 1));
        Assert.Equal([new SagaStatisticsDashboardOrchestration("inventory.stock.updated", 1)],
            await reader.GetTopOrchestrationsAsync(new SagaStatisticsDashboardQuery(now.AddHours(-1), now.AddHours(1), "inventory.stock.updated"), 10));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.GetTopOrchestrationsAsync(query, 0));
        await Assert.ThrowsAsync<ArgumentNullException>(() => reader.GetTopOrchestrationsAsync(null!, 10));
    }

    [Fact]
    public async Task DashboardStatisticsReaderSearchesServerSideAndFiltersByDeployedVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var firstArtifact = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var secondArtifact = RuntimeArtifact(new SemanticVersion(2, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        db.RuntimeOrchestrationArtifacts.AddRange(firstArtifact, secondArtifact);
        await db.SaveChangesAsync();

        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;
        await repository.Create(Instance(firstArtifact.Id, OrchestrationInstanceStatus.Completed, now));
        await repository.Create(Instance(secondArtifact.Id, OrchestrationInstanceStatus.Running, now));
        await scope.ServiceProvider.GetRequiredService<ISagaStatisticsBatchProcessor>().ProcessAsync(20);

        var reader = scope.ServiceProvider.GetRequiredService<ISagaStatisticsDashboardReader>();
        var query = new SagaStatisticsDashboardQuery(now.AddHours(-1), now.AddHours(1), "sales.sale.created", "1.0.0");

        Assert.Equal(1, (await reader.GetCohortAsync(query)).Total);
        Assert.Equal(["sales.sale.created"], await reader.SearchOrchestrationKeysAsync("sale", 10));
        Assert.Equal(["2.0.0", "1.0.0"], await reader.SearchOrchestrationVersionsAsync("sales.sale.created", string.Empty, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.GetCohortAsync(query with { OrchestrationVersion = "invalid" }));
    }

    [Fact]
    public async Task StatisticsBackfillCapturesLegacyInstancesAndPersistsCompletion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var commands = new List<string>();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection)
            .LogTo(commands.Add, [Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted]));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Completed, DateTime.UtcNow);
        instance.CurrentStageKey = "current-stage";
        instance.CurrentTaskKey = "current-task";
        instance.Metadata["keep"] = JsonValue.Create("original")!;
        await repository.Create(instance);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SagaStatisticsObservations";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "UPDATE OrchestrationInstances SET StatisticsRevision = 0, LastExecutedStageKey = '', LastExecutedTaskKey = ''";
        await command.ExecuteNonQueryAsync();
        scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().ChangeTracker.Clear();
        commands.Clear();

        var backfill = scope.ServiceProvider.GetRequiredService<ISagaStatisticsBackfill>();
        Assert.False(await backfill.ProcessAsync(10));
        Assert.True(await backfill.ProcessAsync(10));
        Assert.True(await backfill.ProcessAsync(10));
        Assert.DoesNotContain(commands, x => x.Contains("SnapshotPayload", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(commands, x => x.Contains("Metadata", StringComparison.OrdinalIgnoreCase));
        var restored = await repository.GetById(instance.Id);
        Assert.Equal("S-1", restored.SnapshotPayload!["saleId"]!.GetValue<string>());
        Assert.Equal("original", restored.Metadata["keep"]!.GetValue<string>());
        Assert.Equal("current-stage", restored.LastExecutedStageKey);
        Assert.Equal("current-task", restored.LastExecutedTaskKey);
        command.CommandText = "SELECT COUNT(*) FROM SagaStatisticsObservations WHERE Revision = 1 AND IsBackfill = 1";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT COUNT(*) FROM SagaStatisticsBackfillCheckpoints";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
        Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<ISagaStatisticsBatchProcessor>().ProcessAsync(10)).Applied);
    }

    [Fact]
    public async Task StatisticsBatchProjectsCurrentStatusAndCountsEachReachedStatusOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await repository.Create(instance);
        instance.Status = OrchestrationInstanceStatus.DeadLettered;
        await repository.Update(instance);
        instance.Status = OrchestrationInstanceStatus.Running;
        await repository.Update(instance);
        instance.Status = OrchestrationInstanceStatus.Completed;
        await repository.Update(instance);
        var processor = scope.ServiceProvider.GetRequiredService<ISagaStatisticsBatchProcessor>();
        Assert.Equal(new SagaStatisticsBatchResult(4, 4, 0), await processor.ProcessAsync(100));
        Assert.Equal(new SagaStatisticsBatchResult(0, 0, 0), await processor.ProcessAsync(100));

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Status, Revision FROM SagaStatisticsProjections";
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Completed", reader.GetString(0));
            Assert.Equal(4, reader.GetInt64(1));
        }
        command.CommandText = "SELECT COUNT(*) FROM SagaStatisticsObservations WHERE Status = 'Running' AND StatusFirstReached = 1";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT COUNT(*) FROM SagaStatisticsObservations WHERE Processed = 0";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task StatisticsBatchLeavesAnOutOfOrderObservationPending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await repository.Create(instance);
        instance.Status = OrchestrationInstanceStatus.Completed;
        await repository.Update(instance);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SagaStatisticsObservations WHERE Revision = 1";
        await command.ExecuteNonQueryAsync();
        var result = await scope.ServiceProvider.GetRequiredService<ISagaStatisticsBatchProcessor>().ProcessAsync(100);
        Assert.Equal(new SagaStatisticsBatchResult(1, 0, 1), result);
        command.CommandText = "SELECT COUNT(*) FROM SagaStatisticsObservations WHERE Processed = 0";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task StatisticsObservationsFollowCommittedInstanceRevisions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await repository.Create(instance);
        instance.Status = OrchestrationInstanceStatus.DeadLettered;
        await repository.Update(instance);
        instance.Status = OrchestrationInstanceStatus.Completed;
        await repository.Update(instance);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Revision, Status FROM SagaStatisticsObservations ORDER BY Revision";
        await using var reader = await command.ExecuteReaderAsync();
        var observations = new List<(long Revision, string Status)>();
        while (await reader.ReadAsync()) observations.Add((reader.GetInt64(0), reader.GetString(1)));
        Assert.Equal([(1L, "Running"), (2L, "DeadLettered"), (3L, "Completed")], observations);
        Assert.Equal(3, (await repository.GetById(instance.Id)).StatisticsRevision);
    }

    [Fact]
    public async Task StatisticsObservationRollsBackWithInstanceUpdate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await repository.Create(instance);
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>();
        await using (await unitOfWork.BeginTransactionAsync())
        {
            using (unitOfWork.DeferAutoSave())
            {
                instance.Status = OrchestrationInstanceStatus.Completed;
                await repository.Update(instance);
                await unitOfWork.SaveChanges();
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM SagaStatisticsObservations";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
        using var readScope = provider.CreateScope();
        var persisted = await readScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>().GetById(instance.Id);
        Assert.Equal(OrchestrationInstanceStatus.Running, persisted.Status);
        Assert.Equal(1, persisted.StatisticsRevision);
    }

    [Fact]
    public async Task StaleInstanceUpdateCannotOverwriteACommittedStatisticsRevision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        await first.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var firstRepository = first.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var secondRepository = second.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await firstRepository.Create(instance);
        var stale = await secondRepository.GetById(instance.Id);
        instance.Status = OrchestrationInstanceStatus.Waiting;
        await firstRepository.Update(instance);
        stale.Status = OrchestrationInstanceStatus.Completed;
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => secondRepository.Update(stale));

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM SagaStatisticsObservations";
        Assert.Equal(2L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task RuntimeStorageUnitOfWorkIgnoresRepeatedDeferralDisposal()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>();

        var deferral = unitOfWork.DeferAutoSave();
        Assert.False(unitOfWork.AutoSaveChanges);

        deferral.Dispose();
        deferral.Dispose();

        Assert.True(unitOfWork.AutoSaveChanges);
    }

    [Fact]
    public async Task OrchestrationInstanceRepositoryReturnsOnlyNonTerminalInstancesForBackgroundRecovery()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;
        var failed = Instance(Id.New(), OrchestrationInstanceStatus.Failed, now.AddMinutes(-2));
        var running = Instance(Id.New(), OrchestrationInstanceStatus.Running, now.AddMinutes(-3));
        var deadLettered = Instance(Id.New(), OrchestrationInstanceStatus.DeadLettered, now.AddMinutes(-4));
        await repository.Create(failed);
        await repository.Create(running);
        await repository.Create(deadLettered);

        var recoverable = await repository.GetRecoverable();

        Assert.Equal([running.Id], recoverable.Select(instance => instance.Id).ToArray());
    }

    [Fact]
    public async Task OrchestrationInstanceRepositoryFindsCancellationIntentsOnTerminalInstances()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var intents = Assert.IsAssignableFrom<IOrchestrationDispatchCancellationIntentRepository>(repository);
        var pending = Instance(Id.New(), OrchestrationInstanceStatus.DeadLettered, DateTime.UtcNow.AddMinutes(-1));
        pending.DispatchCancellationPending = true;
        var next = Instance(Id.New(), OrchestrationInstanceStatus.Completed, DateTime.UtcNow);
        next.DispatchCancellationPending = true;
        await repository.Create(pending);
        await repository.Create(next);
        await repository.Create(Instance(Id.New(), OrchestrationInstanceStatus.Completed, DateTime.UtcNow));

        var recovered = await intents.GetDispatchCancellationPending(10, 0);

        Assert.Equal([pending.Id, next.Id], recovered.Select(instance => instance.Id).ToArray());
        Assert.Equal([next.Id], (await intents.GetDispatchCancellationPending(1, 1)).Select(instance => instance.Id).ToArray());
    }

    [Fact]
    public async Task TaskDispatchRepositoryReturnsOnlyPhysicalCancellationsStillPending()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITaskDispatchRepository>();
        var cancellationRepository = Assert.IsAssignableFrom<ITaskDispatchCancellationRepository>(repository);
        var pending = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = Id.New(),
            DispatchType = "Messaging",
            DispatchStatus = "Cancelled",
            PhysicalCancellationPending = true,
            PhysicalCancellationLastAttemptOnUtc = DateTime.UtcNow.AddMinutes(-2)
        };
        var completed = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = Id.New(),
            DispatchType = "Messaging",
            DispatchStatus = "Cancelled",
            PhysicalCancellationPending = false
        };
        var recentlyAttempted = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = Id.New(),
            DispatchType = "Messaging",
            DispatchStatus = "Cancelled",
            PhysicalCancellationPending = true,
            PhysicalCancellationLastAttemptOnUtc = DateTime.UtcNow
        };
        var next = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = Id.New(),
            DispatchType = "Messaging",
            DispatchStatus = "Cancelled",
            PhysicalCancellationPending = true,
            PhysicalCancellationLastAttemptOnUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        await repository.Create(pending);
        await repository.Create(completed);
        await repository.Create(recentlyAttempted);
        await repository.Create(next);

        var pendingCancellations = await cancellationRepository.GetPhysicalCancellationPending(10, 0, DateTime.UtcNow.AddSeconds(-5));

        Assert.Equal([pending.Id, next.Id], pendingCancellations.Select(dispatch => dispatch.Id).ToArray());
        Assert.Equal([next.Id], (await cancellationRepository.GetPhysicalCancellationPending(1, 1, DateTime.UtcNow.AddSeconds(-5)))
            .Select(dispatch => dispatch.Id).ToArray());
    }

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

        instance.Status = OrchestrationInstanceStatus.Waiting;
        await instanceRepository.Update(instance);
        Assert.Equal(
            instance.Id,
            (await instanceRepository.TryGetByStartIdempotencyKey(instance.StartIdempotencyKey))!.Id);
        Assert.Null(await instanceRepository.TryGetByStartIdempotencyKey("missing-start-key"));

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
        var queuePendingDispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = laterAttempt.Id,
            DispatchType = "Messaging",
            DispatchStatus = "QueuePending",
            CommandId = "cmd-queue-pending",
            ScheduledOnUtc = now.AddMinutes(-21)
        };

        await dispatchRepository.Create(dispatch);
        await dispatchRepository.Create(oldDispatch);
        await dispatchRepository.Create(queuePendingDispatch);
        await dispatchRepository.MarkSent(dispatch.Id, "Sent", now.AddMinutes(-8), "external-1");
        await dispatchRepository.MarkFailed(oldDispatch.Id, "broker down", "external-2");
        await dispatchRepository.MarkSent(oldDispatch.Id, "Sent", now.AddMinutes(-7), "external-2");
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
        Assert.Equal(3, (await dispatchRepository.GetScheduledOlderThan(now)).Count);
        Assert.Equal(queuePendingDispatch.Id, Assert.Single(await dispatchRepository.GetQueuePending(1)).Id);
        var failedDispatch = await dispatchRepository.GetById(oldDispatch.Id);
        Assert.Equal("Failed", failedDispatch.DispatchStatus);
        Assert.NotNull(failedDispatch.SentOnUtc);
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
    public async Task OrchestrationInstanceRepositoryAcquiresLeaseAtomicallyAcrossRelationalScopes()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"krackend-runtime-lease-{Guid.NewGuid():N}.db");
        try
        {
            await using var provider = CreateProvider(
                configureDbContext: options => options.UseSqlite($"Data Source={databasePath}"));
            using (var setupScope = provider.CreateScope())
            {
                var dbContext = setupScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var artifactRepository = setupScope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
                var instanceRepository = setupScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
                var artifact = RuntimeArtifact(new SemanticVersion(9, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
                await artifactRepository.Upsert(artifact);
                await instanceRepository.Create(Instance(
                    artifact.Id,
                    OrchestrationInstanceStatus.Running,
                    DateTime.UtcNow.AddMinutes(-1)));
            }

            Id instanceId;
            using (var readScope = provider.CreateScope())
            {
                instanceId = (await readScope.ServiceProvider
                    .GetRequiredService<IOrchestrationInstanceRepository>()
                    .GetRecent(1))
                    .Single()
                    .Id;
            }

            var now = DateTime.UtcNow;
            var acquireTasks = Enumerable.Range(1, 8)
                .Select(async index =>
                {
                    using var acquireScope = provider.CreateScope();
                    var repository = acquireScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
                    return await repository.TryAcquireLease(
                        instanceId,
                        $"lease-{index}",
                        now,
                        now.AddMinutes(1));
                })
                .ToArray();

            var leases = await Task.WhenAll(acquireTasks);
            var winner = Assert.Single(leases.OfType<OrchestrationInstanceLease>());

            using var verifyScope = provider.CreateScope();
            var verifyRepository = verifyScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
            var renewed = await verifyRepository.TryAcquireLease(
                instanceId,
                winner.LeaseId,
                now.AddSeconds(10),
                now.AddMinutes(2));
            var blocked = await verifyRepository.TryAcquireLease(
                instanceId,
                "late-runtime-node",
                now.AddSeconds(20),
                now.AddMinutes(3));
            var persisted = await verifyRepository.GetById(instanceId);

            Assert.NotNull(renewed);
            Assert.Null(blocked);
            Assert.Equal(winner.LeaseId, persisted.ActiveLeaseId);
            Assert.True(persisted.ActiveLeaseExpiresOnUtc.HasValue);
            Assert.True(persisted.ActiveLeaseExpiresOnUtc.Value >= now.AddMinutes(2).AddSeconds(-1));
            persisted.Status = OrchestrationInstanceStatus.Idle;
            await verifyRepository.Update(persisted);
            Assert.Equal(winner.LeaseId, (await verifyRepository.GetById(instanceId)).ActiveLeaseId);
            await verifyRepository.ReleaseLease(instanceId, "wrong-lease");
            Assert.Equal(winner.LeaseId, (await verifyRepository.GetById(instanceId)).ActiveLeaseId);
            await verifyRepository.ReleaseLease(instanceId, winner.LeaseId);
            Assert.Null((await verifyRepository.GetById(instanceId)).ActiveLeaseId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                try
                {
                    File.Delete(databasePath);
                }
                catch (IOException)
                {
                }
            }
        }
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

        var rejectedIngress = Ingress(readyArtifact.Id, "trigger-001", IngressKind.Trigger, """{"topic":"updated"}""");
        rejectedIngress.MessageDisposition = IngressMessageDisposition.Reject;
        await ingressRepository.UpsertForArtifactAsync(readyArtifact.Id, [rejectedIngress]);
        var activeAfterUpsert = await ingressRepository.GetActiveByArtifactIdAsync(readyArtifact.Id);
        Assert.Single(activeAfterUpsert);
        Assert.Equal("""{"topic":"updated"}""", activeAfterUpsert.Single().SettingsPayload);
        Assert.Equal(IngressMessageDisposition.Reject, activeAfterUpsert.Single().MessageDisposition);

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
    public async Task RuntimeArtifactCatalogPagesGroupsVersionsAndHistoryInStorage()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = services.GetRequiredService<IRuntimeArtifactRepository>();
        var catalog = Assert.IsAssignableFrom<IRuntimeArtifactUiCatalog>(repository);
        var historyRepository = services.GetRequiredService<IRuntimeArtifactDeploymentHistoryRepository>();
        var historyCatalog = Assert.IsAssignableFrom<IRuntimeArtifactHistoryCatalog>(historyRepository);
        var first = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var second = RuntimeArtifact(new SemanticVersion(2, 0, 0), RuntimeOrchestrationArtifactStatus.Pending);
        first.DeployedOnUtc = DateTime.UtcNow.AddMinutes(-1); second.DeployedOnUtc = DateTime.UtcNow;
        second.LastReleaseId = "release-current";
        var other = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        other.OrchestrationDefinitionKey = "billing.created";
        await repository.Upsert(first); await repository.Upsert(second); await repository.Upsert(other);
        var policyPages = Assert.IsAssignableFrom<IRuntimeArtifactLifetimePolicyRepository>(repository);
        Assert.Equal(2, (await policyPages.GetLifetimePolicyPage(0, 2)).Count);
        Assert.Single(await policyPages.GetLifetimePolicyPage(2, 2));
        var ingress = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        await ingress.UpsertForArtifactAsync(first.Id, [Ingress(first.Id, "trigger:one"), Ingress(first.Id, "trigger:two")]);
        await Assert.IsAssignableFrom<IRuntimeSelectiveTriggerIngressRepository>(ingress)
            .DeactivateTriggersAsync(first.Id, ["trigger:one"]);
        var groups = await catalog.GetGroups(1, 1, "sales");
        Assert.Equal(1, groups.TotalCount); Assert.Equal(2, Assert.Single(groups.Items).VersionCount);
        var versions = await catalog.GetVersions(first.OrchestrationDefinitionKey, 1, 1, "");
        Assert.Equal(2, versions.TotalCount); Assert.Equal(second.Id, Assert.Single(versions.Items).Id);
        Assert.Equal(first.Id, Assert.Single((await catalog.GetVersions(first.OrchestrationDefinitionKey, 1, 25, "1.0.0")).Items).Id);
        var firstVersion = Assert.Single((await catalog.GetVersions(first.OrchestrationDefinitionKey, 1, 25, "1.0.0")).Items);
        Assert.Equal(1, firstVersion.ActiveTriggerCount);
        Assert.Equal(2, firstVersion.TotalTriggerCount);
        Assert.Equal(second.Id, Assert.Single((await catalog.GetVersions(first.OrchestrationDefinitionKey, 1, 25, "current")).Items).Id);
        Assert.Equal(first.Id, Assert.Single((await catalog.GetVersions(first.OrchestrationDefinitionKey, 1, 25, first.Id.ToString())).Items).Id);
        await historyRepository.Append(new RuntimeArtifactDeploymentRecord { Id = Id.New(), RuntimeOrchestrationArtifactId = first.Id,
            OrchestrationDefinitionKey = first.OrchestrationDefinitionKey, OrchestrationVersion = "1.0.0", ControlPlaneArtifactId = "artifact",
            ArtifactType = "orchestration-version.new", ReleaseId = "release-1", ReleaseStrategy = "Direct", AppliedOnUtc = DateTime.UtcNow });
        var history = await historyCatalog.GetPage(first.Id, 1, 1);
        Assert.Equal(1, history.TotalCount); Assert.Equal("release-1", Assert.Single(history.Items).ReleaseId);
        Assert.Empty((await historyCatalog.GetPage(second.Id, 1, 25)).Items);
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
    public async Task RuntimeDesignNodeCatalogFiltersAndPagesInRelationalStorage()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IRuntimeDesignNodeRepository>();
        var alpha = DesignNode("alpha", "Alpha", DistributionConnectionMode.HybridSync, RuntimeDesignNodeStatus.Pending, false, ConnectionCredentialStatus.Missing);
        var beta = DesignNode("beta", "Beta", DistributionConnectionMode.HybridSync, RuntimeDesignNodeStatus.Pending, false, ConnectionCredentialStatus.Missing);
        await repository.UpsertAsync(alpha);
        await repository.UpsertAsync(beta);
        var catalog = Assert.IsAssignableFrom<IRuntimeDesignNodeUiCatalog>(repository);
        Assert.Equal(2, (await catalog.GetPage(1, 1, "")).TotalCount);
        Assert.Equal("alpha", Assert.Single((await catalog.GetPage(1, 1, "")).Items).Key);
        Assert.Equal("beta", Assert.Single((await catalog.GetPage(2, 1, "")).Items).Key);
        Assert.Equal("beta", Assert.Single((await catalog.GetPage(1, 25, "beta")).Items).Key);
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
    public async Task RuntimeExtensionPackageRepositoryPersistsActivatedBundlesAcrossScopes()
    {
        RuntimeExtensionPackage activated;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using (var setupScope = provider.CreateScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        using (var scope = provider.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRuntimeExtensionPackageRepository>();
            Assert.Contains("EntityFramework", repository.GetType().FullName);
            Assert.Contains("EntityFramework", scope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>().GetType().FullName);
            activated = ExtensionPackage(
                "contoso.billing",
                "task.send-invoice",
                new SemanticVersion(2, 1, 0),
                RuntimeExtensionPackageStatus.Activated);
            await repository.UpsertAsync(activated);
            Assert.Single(await repository.GetAllAsync());
        }

        using var readScope = provider.CreateScope();
        var storedRepository = readScope.ServiceProvider.GetRequiredService<IRuntimeExtensionPackageRepository>();
        Assert.Contains("EntityFramework", storedRepository.GetType().FullName);
        var allPackages = await storedRepository.GetAllAsync();
        var byCapability = await storedRepository.TryGetActiveAsync(
            "CONTOSO.BILLING",
            new SemanticVersion(2, 1, 0));
        var byBundle = await storedRepository.TryGetActiveBundleAsync(
            activated.BundleId.ToUpperInvariant(),
            "CONTOSO.BILLING",
            new SemanticVersion(2, 1, 0),
            activated.Sha256.ToLowerInvariant());

        Assert.Single(allPackages);
        Assert.Equal(activated.Id, byCapability.Id);
        Assert.Equal(activated.Id, byBundle.Id);
        Assert.Equal("task.send-invoice", Assert.Single(byCapability.Manifest.Capabilities).Key.Value);
    }

    [Fact]
    public async Task RuntimeExtensionPackageRepositoryUpdatesExistingPackageMetadata()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRuntimeExtensionPackageRepository>();
        var created = ExtensionPackage(
            "contoso.shipping",
            "task.create-label",
            new SemanticVersion(1, 0, 0),
            RuntimeExtensionPackageStatus.Activated);
        await repository.UpsertAsync(created);

        var updated = ExtensionPackage(
            "contoso.shipping.updated",
            "task.create-label.updated",
            new SemanticVersion(1, 1, 0),
            RuntimeExtensionPackageStatus.Disabled);
        updated.Id = created.Id;
        updated.BundleId = "bundle-updated";
        updated.Sha256 = "DEF456";
        updated.SizeBytes = 8192;
        updated.CreatedOnUtc = created.CreatedOnUtc.AddMinutes(-5);
        updated.UpdatedOnUtc = created.UpdatedOnUtc.AddMinutes(5);
        updated.ActivatedOnUtc = null;
        updated.StatusReason = "manual quarantine";

        await repository.UpsertAsync(updated);

        var stored = Assert.Single(await repository.GetAllAsync());
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal("bundle-updated", stored.BundleId);
        Assert.Equal("contoso.shipping.updated", stored.ExtensionKey);
        Assert.Equal(new SemanticVersion(1, 1, 0), stored.Version);
        Assert.Equal("DEF456", stored.Sha256);
        Assert.Equal(8192, stored.SizeBytes);
        Assert.Equal(RuntimeExtensionPackageStatus.Disabled, stored.Status);
        Assert.Null(stored.ActivatedOnUtc);
        Assert.Equal("manual quarantine", stored.StatusReason);
        Assert.Null(await repository.TryGetActiveAsync("contoso.shipping.updated", new SemanticVersion(1, 1, 0)));
    }

    [Fact]
    public async Task DeferredSaveWithoutTransactionPublishesAfterSave()
    {
        var publisher = new RecordingRuntimeReactiveEventPublisher();
        await using var provider = CreateProvider(publisher);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await services.GetRequiredService<IOrchestrationInstanceRepository>().Create(instance);
        var transitions = services.GetRequiredService<IExecutionTransitionRepository>();
        var unitOfWork = services.GetRequiredService<IRuntimeStorageUnitOfWork>();
        using (unitOfWork.DeferAutoSave())
        {
            await transitions.Create(new ExecutionTransition { Id = Id.New(), OrchestrationInstanceId = instance.Id,
                TransitionType = "InstanceCompleted", OccurredOnUtc = DateTime.UtcNow, ProducedBy = "test" });
            Assert.Empty(publisher.Events);
            await unitOfWork.SaveChanges();
            Assert.Single(publisher.Events);
        }
    }

    [Fact]
    public async Task NotificationFailureDoesNotFailCommittedTransition()
    {
        var publisher = new RecordingRuntimeReactiveEventPublisher { ThrowOnPublish = true };
        await using var provider = CreateProvider(publisher);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await services.GetRequiredService<IOrchestrationInstanceRepository>().Create(instance);
        var transitions = services.GetRequiredService<IExecutionTransitionRepository>();
        await transitions.Create(new ExecutionTransition { Id = Id.New(), OrchestrationInstanceId = instance.Id,
            TransitionType = "InstanceCompleted", OccurredOnUtc = DateTime.UtcNow, ProducedBy = "test" });

        Assert.Single(await transitions.GetByInstanceId(instance.Id));
    }

    [Fact]
    public async Task ReactiveEventsArePublishedOnlyAfterTransactionCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var publisher = new RecordingRuntimeReactiveEventPublisher();
        await using var provider = CreateProvider(publisher, options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await services.GetRequiredService<IOrchestrationInstanceRepository>().Create(instance);
        var transitions = services.GetRequiredService<IExecutionTransitionRepository>();
        var unitOfWork = services.GetRequiredService<IRuntimeStorageUnitOfWork>();

        await using (var transaction = await unitOfWork.BeginTransactionAsync())
        {
            using (unitOfWork.DeferAutoSave())
            {
                await transitions.Create(new ExecutionTransition { Id = Id.New(), OrchestrationInstanceId = instance.Id,
                    TransitionType = "InstanceCompleted", OccurredOnUtc = DateTime.UtcNow, ProducedBy = "test" });
                Assert.Empty(publisher.Events);
                await unitOfWork.SaveChanges();
                Assert.Empty(publisher.Events);
            }
            await transaction.CommitAsync();
            Assert.Single(publisher.Events);
        }
    }

    [Fact]
    public async Task RolledBackTransitionDoesNotPublishReactiveEvent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var publisher = new RecordingRuntimeReactiveEventPublisher();
        await using var provider = CreateProvider(publisher, options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var instance = Instance(Id.New(), OrchestrationInstanceStatus.Running, DateTime.UtcNow);
        await services.GetRequiredService<IOrchestrationInstanceRepository>().Create(instance);
        var transitions = services.GetRequiredService<IExecutionTransitionRepository>();
        var unitOfWork = services.GetRequiredService<IRuntimeStorageUnitOfWork>();

        await using (await unitOfWork.BeginTransactionAsync())
        {
            using (unitOfWork.DeferAutoSave())
            {
                await transitions.Create(new ExecutionTransition { Id = Id.New(), OrchestrationInstanceId = instance.Id,
                    TransitionType = "InstanceCompleted", OccurredOnUtc = DateTime.UtcNow, ProducedBy = "test" });
                await unitOfWork.SaveChanges();
            }
        }

        Assert.Empty(publisher.Events);
        Assert.Empty(await transitions.GetByInstanceId(instance.Id));
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
        instance.OrchestrationVersion = artifact.Version.ToString();
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

    [Fact]
    public async Task ExecutionTransitionRepositoryBuildsMinuteTrafficBucketsForRecentWindows()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var artifactRepository = services.GetRequiredService<IRuntimeArtifactRepository>();
        var instanceRepository = services.GetRequiredService<IOrchestrationInstanceRepository>();
        var transitionRepository = services.GetRequiredService<IExecutionTransitionRepository>();
        var now = DateTime.UtcNow;
        var artifact = RuntimeArtifact(new SemanticVersion(4, 1, 1), RuntimeOrchestrationArtifactStatus.Ready);
        await artifactRepository.Upsert(artifact);
        var running = Instance(artifact.Id, OrchestrationInstanceStatus.Running, now.AddMinutes(-20));
        var completed = Instance(artifact.Id, OrchestrationInstanceStatus.Completed, now.AddMinutes(-18));
        completed.CompletedOnUtc = now.AddMinutes(-3);
        await instanceRepository.Create(running);
        await instanceRepository.Create(completed);

        var traffic = await transitionRepository.GetTraffic(now.AddMinutes(-30));

        Assert.True(traffic.Count >= 2);
        Assert.All(traffic.Zip(traffic.Skip(1)), pair => Assert.Equal(TimeSpan.FromMinutes(1), pair.Second.BucketUtc - pair.First.BucketUtc));
        Assert.Contains(traffic, point => point.Completed > 0);
        Assert.Contains(traffic, point => point.Active > 0);
    }

    private static ServiceProvider CreateProvider(
        IRuntimeReactiveEventPublisher? publisher = null,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        var services = new ServiceCollection();
        var databaseRoot = new InMemoryDatabaseRoot();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(configureDbContext ?? (options =>
            options.UseInMemoryDatabase($"runtime-storage-{Guid.NewGuid():N}", databaseRoot)));
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
            StartIdempotencyKey = $"start:{Guid.NewGuid():N}",
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

    private static RuntimeExtensionPackage ExtensionPackage(
        string extensionKey,
        string capabilityKey,
        SemanticVersion version,
        RuntimeExtensionPackageStatus status)
        => new()
        {
            Id = Id.New(),
            BundleId = $"bundle-{extensionKey}-{version}",
            ExtensionKey = extensionKey,
            Version = version,
            Sha256 = "ABC123",
            SizeBytes = 4096,
            Status = status,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
            ActivatedOnUtc = status == RuntimeExtensionPackageStatus.Activated ? DateTime.UtcNow : null,
            Manifest = new KrackendExtensionManifest(
                new ExtensionKey(extensionKey),
                version,
                extensionKey,
                "tests",
                ExtensionLoadMode.ExternalAssembly,
                ExtensionTrustLevel.PublisherTrusted)
            {
                Capabilities =
                [
                    new ExtensionCapabilityDescriptor(
                        new CapabilityKey(capabilityKey),
                        version,
                        "Task",
                        capabilityKey)
                ]
            }
        };

    private sealed class RecordingRuntimeReactiveEventPublisher : IRuntimeReactiveEventPublisher
    {
        public List<RuntimeReactiveEvent> Events { get; } = [];
        public bool ThrowOnPublish { get; init; }

        public Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
        {
            if (ThrowOnPublish) throw new InvalidOperationException("Notification unavailable.");
            Events.Add(eventData);
            return Task.CompletedTask;
        }
    }

}
