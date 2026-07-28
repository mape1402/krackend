using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Contracts;
using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Streams;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Tests;

public sealed class EventSourcingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddKrackendEventSourcing_adds_pascal_case_default_store_when_no_store_is_configured()
    {
        var services = new ServiceCollection();

        services.AddKrackendEventSourcing();

        using var provider = services.BuildServiceProvider();
        var stores = provider.GetRequiredService<EventStoreOptionsCollection>();

        var store = Assert.Single(stores.Values.Values);
        Assert.Equal("domain", store.Name);
        Assert.Equal("Events", store.TableName);
    }

    [Fact]
    public void AddKrackendEventSourcing_does_not_add_default_store_when_stores_are_configured()
    {
        var services = new ServiceCollection();

        services.AddKrackendEventSourcing(options =>
        {
            options.Stores.Add("orders", store => store.TableName = "OrderEvents");
        });

        using var provider = services.BuildServiceProvider();
        var stores = provider.GetRequiredService<EventStoreOptionsCollection>();

        var store = Assert.Single(stores.Values.Values);
        Assert.Equal("orders", store.Name);
        Assert.Equal("OrderEvents", store.TableName);
    }

    [Fact]
    public async Task AddKrackendEventSourcing_discovers_deciders_reducers_and_attributed_event_types()
    {
        var services = new ServiceCollection();

        services.AddKrackendEventSourcing(options =>
        {
            options.ScanAssemblyContaining<EventSourcingServiceCollectionExtensionsTests>();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var decider = scope.ServiceProvider.GetRequiredService<IEventDecider<TestState, CreateThing>>();
        var reducers = scope.ServiceProvider.GetRequiredService<IEventReducerRegistry>();
        var eventTypes = scope.ServiceProvider.GetRequiredService<IEventTypeRegistry>();

        var events = await decider.DecideAsync(TestState.Empty, new CreateThing("thing-1"));
        var currentState = reducers.Apply(TestState.Empty, events.Single());
        var registration = eventTypes.GetRegistration(typeof(ThingCreated));
        var attributedOnlyEvent = eventTypes.Resolve("ThingArchived", "2.0.0");

        Assert.True(currentState.IsCreated);
        Assert.Equal("thing-1", currentState.Id);
        Assert.Equal("ThingCreated", registration.EventType);
        Assert.Equal(typeof(ThingArchived), attributedOnlyEvent);
    }

    [Fact]
    public async Task EventSourcedApplicationService_executes_with_command_stream_resolver()
    {
        var services = new ServiceCollection();

        services.AddKrackendEventSourcing(options =>
        {
            options.ScanAssemblyContaining<EventSourcingServiceCollectionExtensionsTests>();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IEventSourcedApplicationService<TestState, CreateThing>>();
        var result = await service.ExecuteAsync(TestState.Empty, new CreateThing("thing-1"));

        Assert.Equal(1, result.CurrentVersion);
        Assert.True(result.CurrentState.IsCreated);
        Assert.Equal("thing-1", result.CurrentState.Id);
    }

    private sealed record TestState(string Id, bool IsCreated)
    {
        public static TestState Empty { get; } = new(string.Empty, false);
    }

    private sealed record CreateThing(string Id) : IEventStreamCommand
    {
        public string StreamId => Id;
    }

    [EventSchema("ThingCreated")]
    private sealed record ThingCreated(string Id);

    [EventSchema("ThingArchived", "2.0.0")]
    private sealed record ThingArchived(string Id);

    private sealed class CreateThingDecider : IEventDecider<TestState, CreateThing>
    {
        public ValueTask<IReadOnlyCollection<object>> DecideAsync(
            TestState state,
            CreateThing command,
            CancellationToken cancellationToken = default)
        {
            if (state.IsCreated)
                throw new InvalidOperationException("Thing already exists.");

            return ValueTask.FromResult<IReadOnlyCollection<object>>([new ThingCreated(command.Id)]);
        }
    }

    private sealed class ThingCreatedReducer : IEventReducer<TestState, ThingCreated>
    {
        public TestState Apply(TestState state, ThingCreated @event)
        {
            return state with
            {
                Id = @event.Id,
                IsCreated = true
            };
        }
    }
}
