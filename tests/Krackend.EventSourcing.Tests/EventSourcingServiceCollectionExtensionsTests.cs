using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Streams;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Tests;

public sealed class EventSourcingServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddKrackendEventSourcing_discovers_deciders_reducers_and_event_types()
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

        Assert.True(currentState.IsCreated);
        Assert.Equal("thing-1", currentState.Id);
        Assert.Equal("ThingCreated", registration.EventType);
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

    private sealed record ThingCreated(string Id);

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
