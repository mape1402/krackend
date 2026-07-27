using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.EntityFrameworkCore;
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

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddKrackendEventSourcing(options =>
        {
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

    private sealed record OrderCreated(string OrderId);
}
