using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Streams;
using Krackend.EventSourcing.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Tests;

public sealed class EventSourcingTestingAdapterTests
{
    [Fact]
    public async Task AddKrackendEventSourcingTesting_registers_in_memory_store_and_assertions()
    {
        var services = new ServiceCollection();

        services.AddKrackendEventSourcingTesting();

        using var provider = services.BuildServiceProvider();
        var eventStore = provider.GetRequiredService<IEventSourcingTestEventStore>();
        var assertions = provider.GetRequiredService<IEventSourcingTestAssertions>();
        var stream = EventStreamReference.Create("customers", "customer-001");

        await eventStore.AppendAsync(stream, [new CustomerCreated("customer-001")]);

        await assertions.ShouldHaveEventAsync<CustomerCreated>("customers", "customer-001");
        await assertions.ShouldHaveVersionAsync("customers", "customer-001", 1);
    }

    [Fact]
    public void AddKrackendEventSourcingTestingAdapter_registers_adapter_for_external_hosts()
    {
        var services = new ServiceCollection();

        services.AddKrackendEventSourcingTestingAdapter();

        using var provider = services.BuildServiceProvider();
        var adapter = provider.GetRequiredService<IEventSourcingTestingAdapter>();
        var eventStore = provider.GetRequiredService<IEventSourcingTestEventStore>();
        var assertions = provider.GetRequiredService<IEventSourcingTestAssertions>();

        Assert.Same(eventStore, adapter.EventStore);
        Assert.Same(assertions, adapter.Assertions);
    }

    [Fact]
    public async Task In_memory_store_keeps_order_versions_metadata_and_payload()
    {
        var services = new ServiceCollection();
        services.AddKrackendEventSourcingTestingAdapter();

        using var provider = services.BuildServiceProvider();
        var adapter = provider.GetRequiredService<IEventSourcingTestingAdapter>();
        var stream = EventStreamReference.Create("customers", "customer-001");

        await adapter.EventStore.AppendAsync(
            stream,
            ExpectedVersion.NoStream,
            [
                new CustomerCreated("customer-001"),
                new CustomerRenamed("customer-001", "New Name")
            ],
            new Dictionary<string, object?>
            {
                ["correlation-id"] = "request-001"
            });

        await adapter.Assertions.ShouldHaveStreamAsync("customers", "customer-001");
        await adapter.Assertions.ShouldHaveEventsInOrderAsync(
            "customers",
            "customer-001",
            [typeof(CustomerCreated), typeof(CustomerRenamed)]);
        await adapter.Assertions.ShouldHaveVersionAsync("customers", "customer-001", 2);
        await adapter.Assertions.ShouldHaveMetadataAsync("correlation-id", "request-001");
        await adapter.Assertions.ShouldHaveSerializedPayloadAsync(
            "customers",
            "customer-001",
            """{"CustomerId":"customer-001","Name":"New Name"}""");
    }

    [Fact]
    public async Task In_memory_store_enforces_expected_version()
    {
        var eventStore = new InMemoryEventSourcingTestEventStore();
        var stream = EventStreamReference.Create("customers", "customer-001");

        await eventStore.AppendAsync(stream, ExpectedVersion.NoStream, [new CustomerCreated("customer-001")]);

        var exception = await Assert.ThrowsAsync<EventStoreConcurrencyException>(() =>
            eventStore.AppendAsync(stream, ExpectedVersion.NoStream, [new CustomerRenamed("customer-001", "New Name")]));

        Assert.Equal(0, exception.ExpectedVersion);
        Assert.Equal(1, exception.ActualVersion);
    }

    [Fact]
    public async Task In_memory_store_can_simulate_next_append_concurrency_conflict()
    {
        var eventStore = new InMemoryEventSourcingTestEventStore();
        var stream = EventStreamReference.Create("customers", "customer-001");

        eventStore.FailNextAppendWithConcurrencyConflict(stream);

        await Assert.ThrowsAsync<EventStoreConcurrencyException>(() =>
            eventStore.AppendAsync(stream, [new CustomerCreated("customer-001")]));

        await eventStore.AppendAsync(stream, [new CustomerCreated("customer-001")]);

        var events = await eventStore.ReadAsync(stream);
        Assert.Single(events);
    }

    [Fact]
    public async Task Assertion_failures_throw_event_sourcing_testing_assertion_exception()
    {
        var services = new ServiceCollection();
        services.AddKrackendEventSourcingTesting();

        using var provider = services.BuildServiceProvider();
        var assertions = provider.GetRequiredService<IEventSourcingTestAssertions>();

        await Assert.ThrowsAsync<EventSourcingTestingAssertionException>(() =>
            assertions.ShouldHaveEventAsync<CustomerCreated>("customers", "customer-001"));
    }

    private sealed record CustomerCreated(string CustomerId);

    private sealed record CustomerRenamed(string CustomerId, string Name);
}
