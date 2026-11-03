using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class StatisticsBackfillConcurrencyTests
{
    [Fact]
    public async Task ConcurrentUpdateIsNotOverwrittenOrCountedTwice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var race = new BeforeSave();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite(connection).AddInterceptors(race));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var instance = new OrchestrationInstance
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "orders", CorrelationId = "corr", SagaId = "saga", ExecutionKey = "orders:1",
            Status = OrchestrationInstanceStatus.Running, StartedOnUtc = DateTime.UtcNow, LastUpdatedOnUtc = DateTime.UtcNow,
            SnapshotPayload = JsonNode.Parse("{\"value\":\"original\"}")
        };
        db.OrchestrationInstances.Add(instance);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        race.Action = async cancellationToken =>
        {
            using var concurrent = provider.CreateScope();
            var repository = concurrent.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
            var current = await repository.GetById(instance.Id, cancellationToken);
            current.Status = OrchestrationInstanceStatus.Completed;
            current.SnapshotPayload!["value"] = "concurrent";
            await repository.Update(current, cancellationToken);
        };

        var backfill = scope.ServiceProvider.GetRequiredService<ISagaStatisticsBackfill>();
        Assert.False(await backfill.ProcessAsync(10));
        var saved = await scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>().GetById(instance.Id);
        Assert.Equal(OrchestrationInstanceStatus.Completed, saved.Status);
        Assert.Equal("concurrent", saved.SnapshotPayload!["value"]!.GetValue<string>());
        Assert.Equal(1, saved.StatisticsRevision);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM SagaStatisticsObservations";
        Assert.Equal(1L, await command.ExecuteScalarAsync());

        db.OrchestrationInstances.Add(new OrchestrationInstance
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "other", CorrelationId = "other", SagaId = "other", ExecutionKey = "other:1",
            StartedOnUtc = DateTime.UtcNow, LastUpdatedOnUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        race.Action = _ => Task.FromException(new InvalidOperationException("Simulated storage failure"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => backfill.ProcessAsync(10));
        Assert.Equal("Simulated storage failure", error.Message);
        Assert.False(await backfill.ProcessAsync(10));
        Assert.True(await backfill.ProcessAsync(10));
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
