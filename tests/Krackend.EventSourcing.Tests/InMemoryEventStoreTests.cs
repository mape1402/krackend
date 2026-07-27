using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Stores;

namespace Krackend.EventSourcing.Tests;

public sealed class InMemoryEventStoreTests
{
    [Fact]
    public async Task AppendAsync_stores_events_in_stream_order()
    {
        var store = CreateStore();

        await store.AppendAsync("orders", "order-1", 0, [new OrderCreated("order-1")]);
        await store.AppendAsync("orders", "order-1", 1, [new OrderPaid("order-1")]);

        var events = await store.LoadAsync("orders", "order-1");

        Assert.Collection(
            events,
            first =>
            {
                Assert.Equal("OrderCreated", first.EventType);
                Assert.Equal(1, first.StreamVersion);
            },
            second =>
            {
                Assert.Equal("OrderPaid", second.EventType);
                Assert.Equal(2, second.StreamVersion);
            });
    }

    [Fact]
    public async Task AppendAsync_rejects_conflicting_expected_version()
    {
        var store = CreateStore();

        await store.AppendAsync("orders", "order-1", 0, [new OrderCreated("order-1")]);

        var exception = await Assert.ThrowsAsync<EventStoreConcurrencyException>(() =>
            store.AppendAsync("orders", "order-1", 0, [new OrderPaid("order-1")]));

        Assert.Equal(0, exception.ExpectedVersion);
        Assert.Equal(1, exception.ActualVersion);
    }

    [Fact]
    public async Task AppendAsync_includes_configured_metadata()
    {
        var options = new EventEnvelopeOptions()
            .AddMetadata("tenantId", _ => "tenant-a");

        var store = CreateStore(options);

        var envelopes = await store.AppendAsync("orders", "order-1", 0, [new OrderCreated("order-1")]);

        Assert.Contains("\"tenantId\":\"tenant-a\"", envelopes.Single().Metadata);
    }

    [Fact]
    public async Task AppendAsync_with_any_expected_version_appends_without_loading_stream_events()
    {
        var store = CreateStore();

        await store.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);
        await store.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderPaid("order-1")]);

        Assert.Equal(2, await store.GetCurrentVersionAsync("orders", "order-1"));
    }

    [Fact]
    public async Task ReadStreamAsync_reads_from_requested_version_with_limit()
    {
        var store = CreateStore();

        await store.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);
        await store.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderPaid("order-1")]);
        await store.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderPaid("order-1")]);

        var envelopes = await store.ReadStreamAsync("orders", "order-1", fromVersion: 2, maxCount: 1);

        Assert.Single(envelopes);
        Assert.Equal(2, envelopes.Single().StreamVersion);
        Assert.Equal("OrderPaid", envelopes.Single().EventType);
    }

    [Fact]
    public async Task AppendAsync_with_no_stream_rejects_existing_stream()
    {
        var store = CreateStore();

        await store.AppendAsync("orders", "order-1", ExpectedVersion.NoStream, [new OrderCreated("order-1")]);

        var exception = await Assert.ThrowsAsync<EventStoreConcurrencyException>(() =>
            store.AppendAsync("orders", "order-1", ExpectedVersion.NoStream, [new OrderPaid("order-1")]));

        Assert.Equal(0, exception.ExpectedVersion);
        Assert.Equal(1, exception.ActualVersion);
    }

    private static InMemoryEventStore CreateStore(EventEnvelopeOptions? options = null)
    {
        var registry = new EventTypeRegistry()
            .Register<OrderCreated>()
            .Register<OrderPaid>();

        var serializer = new SystemTextJsonEventSerializer();
        var collector = new EventMetadataCollector(options ?? new EventEnvelopeOptions(), new EmptyServiceProvider());
        var factory = new EventEnvelopeFactory(registry, serializer, collector);

        return new InMemoryEventStore(factory);
    }

    private sealed record OrderCreated(string OrderId);

    private sealed record OrderPaid(string OrderId);

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
