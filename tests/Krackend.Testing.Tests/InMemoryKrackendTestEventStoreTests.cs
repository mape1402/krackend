using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Streams;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Testing.Tests;

public sealed class InMemoryKrackendTestEventStoreTests
{
    [Fact]
    public async Task AddKrackendTesting_registers_in_memory_event_store()
    {
        var services = new ServiceCollection();

        services.AddKrackendTesting();

        using var provider = services.BuildServiceProvider();
        var eventStore = provider.GetRequiredService<IKrackendTestEventStore>();

        var stream = EventStreamReference.Create("customers", "customer-001");
        await eventStore.AppendAsync(stream, [new CustomerCreated("customer-001")]);

        var events = await eventStore.ReadAsync(stream);

        Assert.Single(events);
        Assert.IsType<InMemoryKrackendTestEventStore>(eventStore);
    }

    [Fact]
    public void AddKrackendTestingAdapter_registers_adapter_for_external_test_hosts()
    {
        var services = new ServiceCollection();

        services.AddKrackendTestingAdapter();

        using var provider = services.BuildServiceProvider();
        var adapter = provider.GetRequiredService<IKrackendTestingAdapter>();
        var eventStore = provider.GetRequiredService<IKrackendTestEventStore>();

        Assert.Same(eventStore, adapter.EventStore);
    }

    [Fact]
    public async Task AppendAsync_and_ReadAsync_keep_event_order_and_versions()
    {
        var eventStore = new InMemoryKrackendTestEventStore();
        var stream = EventStreamReference.Create("customers", "customer-001");

        await eventStore.AppendAsync(stream, [new CustomerCreated("customer-001")]);
        await eventStore.AppendAsync(stream, [new CustomerRenamed("customer-001", "New Name")]);

        var events = await eventStore.ReadAsync(stream);

        Assert.Collection(
            events,
            first =>
            {
                Assert.Equal(1, first.StreamVersion);
                Assert.Equal(typeof(CustomerCreated), first.EventClrType);
            },
            second =>
            {
                Assert.Equal(2, second.StreamVersion);
                Assert.Equal(typeof(CustomerRenamed), second.EventClrType);
            });
    }

    [Fact]
    public async Task AppendAsync_enforces_expected_version()
    {
        var eventStore = new InMemoryKrackendTestEventStore();
        var stream = EventStreamReference.Create("customers", "customer-001");

        await eventStore.AppendAsync(stream, ExpectedVersion.NoStream, [new CustomerCreated("customer-001")]);

        var exception = await Assert.ThrowsAsync<EventStoreConcurrencyException>(() =>
            eventStore.AppendAsync(stream, ExpectedVersion.NoStream, [new CustomerRenamed("customer-001", "New Name")]));

        Assert.Equal(0, exception.ExpectedVersion);
        Assert.Equal(1, exception.ActualVersion);
    }

    [Fact]
    public async Task FailNextAppendWithConcurrencyConflict_fails_only_next_append_for_stream()
    {
        var eventStore = new InMemoryKrackendTestEventStore();
        var stream = EventStreamReference.Create("customers", "customer-001");

        eventStore.FailNextAppendWithConcurrencyConflict(stream);

        await Assert.ThrowsAsync<EventStoreConcurrencyException>(() =>
            eventStore.AppendAsync(stream, [new CustomerCreated("customer-001")]));

        await eventStore.AppendAsync(stream, [new CustomerCreated("customer-001")]);

        eventStore.ShouldHaveVersion("customers", "customer-001", 1);
    }

    [Fact]
    public async Task Assertions_validate_stream_event_order_version_metadata_and_payload()
    {
        var eventStore = new InMemoryKrackendTestEventStore();
        var stream = EventStreamReference.Create("customers", "customer-001");

        await eventStore.AppendAsync(
            stream,
            [
                new CustomerCreated("customer-001"),
                new CustomerRenamed("customer-001", "New Name")
            ],
            new Dictionary<string, object?>
            {
                ["correlation-id"] = "request-001"
            });

        eventStore
            .ShouldHaveStream("customers", "customer-001")
            .ShouldHaveEvent<CustomerCreated>("customers", "customer-001")
            .ShouldHaveEventsInOrder("customers", "customer-001", typeof(CustomerCreated), typeof(CustomerRenamed))
            .ShouldHaveVersion("customers", "customer-001", 2)
            .ShouldHaveMetadata("correlation-id", "request-001")
            .ShouldHaveSerializedPayload("customers", "customer-001", """{"CustomerId":"customer-001","Name":"New Name"}""");
    }

    [Fact]
    public void Assertion_failures_throw_krackend_testing_assertion_exception()
    {
        var eventStore = new InMemoryKrackendTestEventStore();

        Assert.Throws<KrackendTestingAssertionException>(() =>
            eventStore.ShouldHaveEvent<CustomerCreated>("customers", "customer-001"));
    }

    private sealed record CustomerCreated(string CustomerId);

    private sealed record CustomerRenamed(string CustomerId, string Name);
}
