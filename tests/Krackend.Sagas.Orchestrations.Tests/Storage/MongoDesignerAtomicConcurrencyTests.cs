namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using DotNet.Testcontainers.Builders;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

public sealed class MongoDesignerAtomicConcurrencyTests
{
    [Fact(Timeout = 180_000)]
    public async Task ConcurrentEditorsCannotOverwriteEachOtherInMongo()
    {
        await using var mongo = new ContainerBuilder().WithImage("mongo:8.0.4").WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(27017)).Build();
        await mongo.StartAsync();
        var connection = $"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}/?directConnection=true";
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseMongoDB(connection, "designer_atomic_tests").Options;
        var taskId = Id.New();
        await using (var setup = new ControlPlaneDbContext(options))
        {
            setup.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
            await setup.Database.EnsureCreatedAsync();
            setup.TaskDefinitions.Add(new TaskDefinitionEntity
            {
                Id = taskId, StageDefinitionId = Id.New(), Key = "task", Name = "Task", Kind = TaskKind.Messaging,
                ExecutionMode = TaskExecutionMode.Sequential, DispatchType = TaskDispatchType.FireAndWaitCallback
            });
            await setup.SaveChangesAsync();
        }

        var gate = new DesignerAtomicConcurrencyTests.SaveGate();
        var waitingOptions = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseMongoDB(connection, "designer_atomic_tests").AddInterceptors(gate).Options;
        await using var first = new ControlPlaneDbContext(options);
        await using var second = new ControlPlaneDbContext(waitingOptions);
        first.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        second.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        var expected = OrchestrationDesignerRevision.FromDsl(string.Empty);
        var delayed = DesignerAtomicConcurrencyTests.Write(second, "task-transform", "second", expected, taskId, Id.New(), Id.New());
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await DesignerAtomicConcurrencyTests.Write(first, "task-transform", "first", expected, taskId, Id.New(), Id.New());
        }
        finally
        {
            gate.Release.TrySetResult();
        }
        await Assert.ThrowsAsync<OrchestrationDesignerConcurrencyException>(() => delayed);
        await using var verify = new ControlPlaneDbContext(options);
        verify.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        Assert.Equal("first", await DesignerAtomicConcurrencyTests.Read(verify, "task-transform", taskId, Id.New(), Id.New()));

        // Simulate a document written before the token existed, followed by the host's backfill.
        var documents = new MongoClient(connection).GetDatabase("designer_atomic_tests").GetCollection<BsonDocument>("TaskDefinitions");
        var removed = await documents.UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, Builders<BsonDocument>.Update.Unset("ConcurrencyToken"));
        Assert.Equal(1, removed.ModifiedCount);
        var backfilled = await documents.UpdateManyAsync(Builders<BsonDocument>.Filter.Exists("ConcurrencyToken", false), Builders<BsonDocument>.Update.Set("ConcurrencyToken", string.Empty));
        Assert.Equal(1, backfilled.ModifiedCount);
        await using var upgraded = new ControlPlaneDbContext(options);
        upgraded.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await DesignerAtomicConcurrencyTests.Write(upgraded, "task-transform", "after-upgrade", OrchestrationDesignerRevision.FromDsl("first"), taskId, Id.New(), Id.New());
        await using var afterUpgrade = new ControlPlaneDbContext(options);
        afterUpgrade.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        Assert.Equal("after-upgrade", await DesignerAtomicConcurrencyTests.Read(afterUpgrade, "task-transform", taskId, Id.New(), Id.New()));
    }
}
