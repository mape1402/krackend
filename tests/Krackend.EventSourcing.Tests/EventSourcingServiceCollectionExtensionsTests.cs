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
        services.AddEventSourcedInitialState(() => TestState.Empty);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var decider = scope.ServiceProvider.GetRequiredService<IEventDecider<TestState, CreateThing>>();
        var reducers = scope.ServiceProvider.GetRequiredService<IEventReducerRegistry>();
        var eventTypes = scope.ServiceProvider.GetRequiredService<IEventTypeRegistry>();
        var stateSchemas = scope.ServiceProvider.GetRequiredService<IStateSchemaRegistry>();

        var events = await decider.DecideAsync(TestState.Empty, new CreateThing("thing-1"));
        var currentState = reducers.Apply(TestState.Empty, events.Single());
        var registration = eventTypes.GetRegistration(typeof(ThingCreated));
        var stateRegistration = stateSchemas.GetRegistration(typeof(TestState));
        var attributedOnlyEvent = eventTypes.Resolve("ThingArchived", "2.0.0");

        Assert.True(currentState.IsCreated);
        Assert.Equal("thing-1", currentState.Id);
        Assert.Equal("ThingCreated", registration.EventType);
        Assert.Equal("TestState", stateRegistration.StateType);
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
        services.AddEventSourcedInitialState(() => TestState.Empty);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IEventSourcedApplicationService<TestState, CreateThing>>();
        var result = await service.ExecuteAsync(new CreateThing("thing-1"));

        Assert.Equal(1, result.CurrentVersion);
        Assert.True(result.CurrentState.IsCreated);
        Assert.Equal("thing-1", result.CurrentState.Id);
    }

    [Fact]
    public void DefaultCommandStreamResolver_uses_event_stream_attribute_for_stream_name()
    {
        var services = new ServiceCollection();

        services.AddKrackendEventSourcing(options =>
        {
            options.Routing.DefaultStreamName = "fallback";
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolver = scope.ServiceProvider.GetRequiredService<ICommandStreamResolver<AttributedCreateThing>>();
        var stream = resolver.Resolve(new AttributedCreateThing("thing-1"));

        Assert.Equal("things", stream.Name);
        Assert.Equal("thing-1", stream.Id);
    }

    [Fact]
    public async Task AddKrackendEventSourcing_discovers_initial_state_factories()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new InitialStateSeed("discovered"));
        services.AddKrackendEventSourcing(options =>
        {
            options.ScanAssemblyContaining<EventSourcingServiceCollectionExtensionsTests>();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IInitialStateFactory<DiscoveredState>>();

        var state = await factory.CreateAsync();

        Assert.Equal("discovered", state.Id);
    }

    [Fact]
    public async Task AddEventSourcedInitialStateFactory_registers_factory_type_with_dependencies()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new InitialStateSeed("seeded"));
        services.AddKrackendEventSourcing(options =>
        {
            options.ScanAssemblyContaining<EventSourcingServiceCollectionExtensionsTests>();
        });
        services.AddEventSourcedInitialStateFactory<TestState, SeededInitialStateFactory>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IInitialStateFactory<TestState>>();

        var state = await factory.CreateAsync();

        Assert.Equal("seeded", state.Id);
    }

    [Fact]
    public async Task AddEventSourcedInitialStateFactory_registers_delegate_with_service_provider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new InitialStateSeed("delegated"));
        services.AddKrackendEventSourcing();
        services.AddEventSourcedInitialStateFactory<TestState>((provider, _) =>
        {
            var seed = provider.GetRequiredService<InitialStateSeed>();

            return ValueTask.FromResult(new TestState(seed.Id, IsCreated: false));
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IInitialStateFactory<TestState>>();

        var state = await factory.CreateAsync();

        Assert.Equal("delegated", state.Id);
    }

    [StateSchema("TestState")]
    private sealed record TestState(string Id, bool IsCreated)
    {
        public static TestState Empty { get; } = new(string.Empty, false);
    }

    private sealed record DiscoveredState(string Id);

    private sealed record InitialStateSeed(string Id);

    private sealed class SeededInitialStateFactory : IInitialStateFactory<TestState>
    {
        private readonly InitialStateSeed _seed;

        public SeededInitialStateFactory(InitialStateSeed seed)
        {
            _seed = seed;
        }

        public ValueTask<TestState> CreateAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new TestState(_seed.Id, IsCreated: false));
        }
    }

    private sealed class DiscoveredInitialStateFactory : IInitialStateFactory<DiscoveredState>
    {
        private readonly InitialStateSeed _seed;

        public DiscoveredInitialStateFactory(InitialStateSeed seed)
        {
            _seed = seed;
        }

        public ValueTask<DiscoveredState> CreateAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new DiscoveredState(_seed.Id));
        }
    }

    private sealed record CreateThing(string Id) : IEventStreamCommand
    {
        public string StreamId => Id;
    }

    [EventStream("things")]
    private sealed record AttributedCreateThing(string Id) : IEventStreamCommand
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
