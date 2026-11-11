using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Mongo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class MongoStatisticsBackfillTests
{
    [Fact(Timeout = 180_000)]
    public async Task BackfillPreservesInstanceDataOnReplicaSet()
    {
        await using var mongo = new ContainerBuilder().WithImage("mongo:8.0.4")
            .WithCommand("--replSet", "rs0", "--bind_ip_all").WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(27017)).Build();
        await mongo.StartAsync();
        var initialized = await mongo.ExecAsync(["mongosh", "--quiet", "--eval", "rs.initiate({_id:'rs0',members:[{_id:0,host:'localhost:27017'}]})"]);
        Assert.Equal(0, initialized.ExitCode);
        var client = new MongoClient($"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}/?directConnection=true&serverSelectionTimeoutMS=1000");
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (true)
        {
            try
            {
                var hello = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
                if (hello.GetValue("isWritablePrimary", false).ToBoolean()) break;
            }
            catch (MongoException) when (DateTime.UtcNow < deadline) { }
            catch (TimeoutException) when (DateTime.UtcNow < deadline) { }
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("Mongo replica set did not elect a primary.");
            await Task.Delay(250);
        }
        var services = new ServiceCollection();
        var race = new BeforeSave();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseMongoDB(client, "statistics_backfill_test").AddInterceptors(race),
            storage => storage.ConfigureModel = model => model.UseKrackendMongoRuntimeModel());
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var instance = new OrchestrationInstance
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "orders", CorrelationId = "corr", SagaId = "saga", ExecutionKey = "orders:1", StartIdempotencyKey = "start-orders",
            CurrentStageKey = "stage", CurrentTaskKey = "task", StartedOnUtc = now, LastUpdatedOnUtc = now,
            OrchestrationVersion = "1.0.0",
            SnapshotPayload = JsonNode.Parse("{\"value\":\"original\"}"),
            Metadata = { ["keep"] = JsonValue.Create("original")! }, StatisticsRevision = 0
        };
        db.OrchestrationInstances.Add(instance);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var backfill = scope.ServiceProvider.GetRequiredService<ISagaStatisticsBackfill>();
        Assert.False(await backfill.ProcessAsync(10));
        var concurrentInstance = new OrchestrationInstance
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "other", CorrelationId = "other", SagaId = "other", ExecutionKey = "other:1", StartIdempotencyKey = "start-other",
            StartedOnUtc = now, LastUpdatedOnUtc = now, SnapshotPayload = JsonNode.Parse("{\"value\":\"old\"}")
        };
        db.OrchestrationInstances.Add(concurrentInstance);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        race.Action = async cancellationToken =>
        {
            using var concurrent = provider.CreateScope();
            var repository = concurrent.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
            var changed = await repository.GetById(concurrentInstance.Id, cancellationToken);
            changed.Status = OrchestrationInstanceStatus.Completed;
            changed.SnapshotPayload!["value"] = "concurrent";
            await repository.Update(changed, cancellationToken);
        };
        Assert.False(await backfill.ProcessAsync(10));
        Assert.True(await backfill.ProcessAsync(10));
        Assert.True(await backfill.ProcessAsync(10));
        var saved = await scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>().GetById(instance.Id);
        Assert.Equal("original", saved.SnapshotPayload!["value"]!.GetValue<string>());
        Assert.Equal("original", saved.Metadata["keep"]!.GetValue<string>());
        Assert.Equal("stage", saved.LastExecutedStageKey);
        Assert.Equal("task", saved.LastExecutedTaskKey);
        Assert.Equal(1, saved.StatisticsRevision);
        var concurrentSaved = await scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>().GetById(concurrentInstance.Id);
        Assert.Equal(OrchestrationInstanceStatus.Completed, concurrentSaved.Status);
        Assert.Equal("concurrent", concurrentSaved.SnapshotPayload!["value"]!.GetValue<string>());
        Assert.Equal(1, concurrentSaved.StatisticsRevision);
        var observations = client.GetDatabase("statistics_backfill_test").GetCollection<BsonDocument>("SagaStatisticsObservations");
        Assert.Equal(2, await observations.CountDocumentsAsync(new BsonDocument("IsBackfill", true)));

        var failureTime = DateTime.UtcNow.AddMinutes(-1);
        db.ExecutionTransitions.AddRange(Enumerable.Range(0, 3).Select(_ => new ExecutionTransition
        {
            Id = Id.New(), OrchestrationInstanceId = instance.Id, OccurredOnUtc = failureTime, TransitionType = "TaskTimedOut"
        }));
        await db.SaveChangesAsync();
        var failures = scope.ServiceProvider.GetRequiredService<ISagaFailureStatisticsBackfill>();
        for (var i = 0; i < 8 && !await failures.ProcessAsync(2); i++) { }
        var failureReport = await scope.ServiceProvider.GetRequiredService<ISagaFailureStatisticsReader>()
            .GetFailuresAsync(failureTime.AddSeconds(-1), failureTime.AddSeconds(1));
        Assert.True(failureReport.HistoryComplete);
        Assert.Equal(3, Assert.Single(failureReport.Items).Count);
        var failureEvents = client.GetDatabase("statistics_backfill_test").GetCollection<BsonDocument>("SagaFailureEvents");
        await failureEvents.UpdateManyAsync(Builders<BsonDocument>.Filter.Empty,
            Builders<BsonDocument>.Update.Unset("ScopeProjectionVersion").Unset("OrchestrationKey").Unset("OrchestrationVersion"));
        db.ChangeTracker.Clear();
        var scopeBackfill = scope.ServiceProvider.GetRequiredService<ISagaFailureScopeBackfill>();
        for (var i = 0; i < 8 && !await scopeBackfill.ProcessAsync(2); i++) { }
        var scopedFailures = await Assert.IsAssignableFrom<ISagaFailureScopedStatisticsReader>(
            scope.ServiceProvider.GetRequiredService<ISagaFailureStatisticsReader>())
            .GetFailuresAsync(failureTime.AddSeconds(-1), failureTime.AddSeconds(1), "orders", "1.0.0");
        Assert.True(scopedFailures.HistoryComplete);
        Assert.Equal(3, Assert.Single(scopedFailures.Items).Count);
        Assert.Equal(0, scopedFailures.UnclassifiedCount);

        var catalog = Assert.IsAssignableFrom<IRuntimeInstanceCatalogRepository>(
            scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>());
        var completed = await catalog.GetPage(new RuntimeInstanceCatalogQuery
        {
            Statuses = [OrchestrationInstanceStatus.Completed]
        });
        Assert.Equal(1, completed.TotalCount);
        Assert.Equal(concurrentInstance.Id, Assert.Single(completed.Items).Id);
        var counts = await Assert.IsAssignableFrom<IRuntimeInstanceStatusCountsRepository>(catalog)
            .GetStatusCounts(new RuntimeInstanceCatalogQuery());
        Assert.Equal(1, counts[OrchestrationInstanceStatus.Completed]);
    }

    private sealed class BeforeSave : SaveChangesInterceptor
    {
        public Func<CancellationToken, Task>? Action { get; set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var action = Action;
            Action = null;
            if (action is not null) await action(cancellationToken);
            return result;
        }
    }
}
