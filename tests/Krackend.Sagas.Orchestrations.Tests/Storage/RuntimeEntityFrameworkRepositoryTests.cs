using System.Text.Json.Nodes;
using System.IO;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Extensions;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class RuntimeEntityFrameworkRepositoryTests
{
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
    public async Task StatisticsBackfillCapturesLegacyInstancesAndPersistsCompletion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(configureDbContext: options => options.UseSqlite(connection));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        await repository.Create(Instance(Id.New(), OrchestrationInstanceStatus.Completed, DateTime.UtcNow));
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SagaStatisticsObservations";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "UPDATE OrchestrationInstances SET StatisticsRevision = 0";
        await command.ExecuteNonQueryAsync();
        scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().ChangeTracker.Clear();

        var backfill = scope.ServiceProvider.GetRequiredService<ISagaStatisticsBackfill>();
        Assert.False(await backfill.ProcessAsync(10));
        Assert.True(await backfill.ProcessAsync(10));
        Assert.True(await backfill.ProcessAsync(10));
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
    public async Task OrchestrationInstanceRepositoryReturnsFailedInstancesForRecovery()
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

        Assert.Equal([running.Id, failed.Id], recoverable.Select(instance => instance.Id).ToArray());
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

        var acquired = await instanceRepository.TryAcquireLease(
            instance.Id,
            "lease-1",
            now,
            now.AddMinutes(5));
        instance.Status = OrchestrationInstanceStatus.Waiting;
        await instanceRepository.Update(instance);
        Assert.Equal("lease-1", (await instanceRepository.GetById(instance.Id)).ActiveLeaseId);

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
        var groups = await catalog.GetGroups(1, 1, "sales");
        Assert.Equal(1, groups.TotalCount); Assert.Equal(2, Assert.Single(groups.Items).VersionCount);
        var versions = await catalog.GetVersions(first.OrchestrationDefinitionKey, 1, 1, "");
        Assert.Equal(2, versions.TotalCount); Assert.Equal(second.Id, Assert.Single(versions.Items).Id);
        Assert.Equal(first.Id, Assert.Single((await catalog.GetVersions(first.OrchestrationDefinitionKey, 1, 25, "1.0.0")).Items).Id);
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
