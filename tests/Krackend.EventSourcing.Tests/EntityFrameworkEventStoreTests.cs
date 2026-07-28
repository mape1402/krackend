using Krackend.EventSourcing.Contracts;
using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.EntityFrameworkCore;
using Krackend.EventSourcing.Snapshots;
using Krackend.EventSourcing.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Tests;

public sealed class EntityFrameworkEventStoreTests
{
    [Fact]
    public void AddKrackendEntityFrameworkEventStore_adds_store_entities_to_app_db_context_model()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Assert.NotNull(dbContext.Model.FindEntityType("orders"));
        Assert.NotNull(dbContext.Model.FindEntityType("payments"));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(EventSnapshotRecord)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(SnapshotCandidateRecord)));
    }

    [Fact]
    public async Task AppendAsync_persists_events_in_app_db_context_event_store()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        await eventStore.AppendAsync("orders", "order-1", 0, [new OrderCreated("order-1")]);

        var envelopes = await eventStore.LoadAsync("orders", "order-1");
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Assert.Single(envelopes);
        Assert.Equal("OrderCreated", envelopes.Single().EventType);
        Assert.Equal(1, await dbContext.Set<EventStoreRecord>("orders").CountAsync());
    }

    [Fact]
    public async Task AppendAsync_with_any_expected_version_appends_and_updates_current_version()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        await eventStore.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);
        await eventStore.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);

        Assert.Equal(2, await eventStore.GetCurrentVersionAsync("orders", "order-1"));
    }

    [Fact]
    public async Task ReadStreamAsync_reads_stream_range_without_loading_full_stream()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        await eventStore.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);
        await eventStore.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);
        await eventStore.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);

        var envelopes = await eventStore.ReadStreamAsync("orders", "order-1", fromVersion: 2, maxCount: 1);

        Assert.Single(envelopes);
        Assert.Equal(2, envelopes.Single().StreamVersion);
    }

    [Fact]
    public void EventStoreDbContextFactory_uses_the_scoped_app_db_context()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<Krackend.EventSourcing.EntityFrameworkCore.IEventStoreDbContextFactory<TestDbContext>>();

        Assert.Same(dbContext, factory.CreateDbContext());
    }

    [Fact]
    public async Task EntityFramework_snapshot_stores_use_app_db_context_model()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var snapshotStore = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();
        var candidateStore = scope.ServiceProvider.GetRequiredService<ISnapshotCandidateStore>();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await snapshotStore.SaveAsync(new Snapshot(
            "orders",
            "order-1",
            3,
            "{}",
            DateTimeOffset.UtcNow));
        await candidateStore.MarkAsync("orders", "order-1", 4);

        Assert.Equal(1, await dbContext.Set<EventSnapshotRecord>().CountAsync());
        Assert.Equal(1, await dbContext.Set<SnapshotCandidateRecord>().CountAsync());
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddKrackendEventSourcing(options =>
        {
            options.ScanAssemblyContaining<EntityFrameworkEventStoreTests>();
            options.Stores.Add("orders", store => store.TableName = "OrderEvents");
            options.Stores.Add("payments", store => store.TableName = "PaymentEvents");
        });

        services.AddKrackendEntityFrameworkEventStore<TestDbContext>();

        return services.BuildServiceProvider();
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options)
            : base(options)
        {
        }
    }

    [EventSchema("OrderCreated")]
    private sealed record OrderCreated(string OrderId);
}
