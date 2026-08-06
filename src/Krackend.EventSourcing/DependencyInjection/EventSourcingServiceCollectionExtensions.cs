using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Snapshots;
using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Krackend.EventSourcing.DependencyInjection;

/// <summary>
/// Provides dependency injection extensions for Krackend event sourcing.
/// </summary>
public static class EventSourcingServiceCollectionExtensions
{
    /// <summary>
    /// Registers Krackend event sourcing services.
    /// </summary>
    public static IServiceCollection AddKrackendEventSourcing(
        this IServiceCollection services,
        Action<EventSourcingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new EventSourcingOptions();
        configure?.Invoke(options);

        if (options.Stores.Values.Count == 0)
            options.Stores.Add(options.Routing.DefaultStreamName);

        if (options.Assemblies.Count == 0 && Assembly.GetEntryAssembly() is { } entryAssembly)
            options.ScanAssembly(entryAssembly);

        var eventTypeRegistry = new EventTypeRegistry();
        var stateSchemaRegistry = new StateSchemaRegistry();

        services.AddSingleton(options);
        services.AddSingleton(options.Envelope);
        services.AddSingleton(options.Routing);
        services.AddSingleton(options.Stores);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddSingleton(eventTypeRegistry);
        services.AddSingleton<IStateSchemaRegistry>(stateSchemaRegistry);
        services.AddSingleton(stateSchemaRegistry);
        services.AddSingleton<IEventSerializer, SystemTextJsonEventSerializer>();
        services.AddSingleton<IEventStreamResolver, ConfiguredEventStreamResolver>();
        services.AddSingleton<ISnapshotStore, InMemorySnapshotStore>();
        services.AddSingleton<ISnapshotSerializer, SystemTextJsonSnapshotSerializer>();
        services.AddSingleton<ISnapshotStrategy, NeverSnapshotStrategy>();
        services.AddSingleton<ISnapshotCandidateStore, InMemorySnapshotCandidateStore>();
        services.AddSingleton<ISnapshotCandidatePolicy, NeverSnapshotCandidatePolicy>();
        services.AddScoped<ISnapshotCandidateMarker, SnapshotCandidateMarker>();
        services.AddScoped(typeof(ISnapshotProcessor<>), typeof(SnapshotProcessor<>));
        services.AddScoped<IEventReducerRegistry>(provider =>
        {
            var registry = new EventReducerRegistry();
            EventSourcingAssemblyScanner.RegisterReducers(provider, registry);
            return registry;
        });
        services.AddScoped<EventMetadataCollector>();
        services.AddScoped<IEventEnvelopeFactory, EventEnvelopeFactory>();
        services.AddScoped<InMemoryEventStore>();
        services.AddScoped<IEventStore>(provider => provider.GetRequiredService<InMemoryEventStore>());
        services.AddScoped<IRawEventStore>(provider => provider.GetRequiredService<InMemoryEventStore>());
        services.AddScoped<IEventLogReader>(provider => provider.GetRequiredService<InMemoryEventStore>());
        services.AddScoped<IStateRehydrator, StateRehydrator>();
        services.AddScoped(typeof(ICommandStreamResolver<>), typeof(DefaultCommandStreamResolver<>));
        services.TryAddScoped(typeof(IInitialStateFactory<>), typeof(MissingInitialStateFactory<>));
        services.AddScoped(typeof(IEventSourcedApplicationService<,>), typeof(EventSourcedApplicationService<,>));
        EventSourcingAssemblyScanner.RegisterComponents(services, eventTypeRegistry, stateSchemaRegistry, options.Assemblies);

        return services;
    }

    /// <summary>
    /// Registers the initial state used by event-sourced application services for a state type.
    /// </summary>
    public static IServiceCollection AddEventSourcedInitialState<TState>(
        this IServiceCollection services,
        Func<TState> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        return services.AddEventSourcedInitialState<TState>((_, _) => ValueTask.FromResult(factory()));
    }

    /// <summary>
    /// Registers the initial state used by event-sourced application services for a state type.
    /// </summary>
    public static IServiceCollection AddEventSourcedInitialState<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, TState> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        return services.AddEventSourcedInitialState<TState>((provider, _) => ValueTask.FromResult(factory(provider)));
    }

    /// <summary>
    /// Registers the initial state used by event-sourced application services for a state type.
    /// </summary>
    public static IServiceCollection AddEventSourcedInitialState<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, CancellationToken, ValueTask<TState>> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.AddScoped<IInitialStateFactory<TState>>(provider =>
            new DelegateInitialStateFactory<TState>(factory, provider));

        return services;
    }

    /// <summary>
    /// Registers the factory that creates the initial state for a state type.
    /// </summary>
    public static IServiceCollection AddEventSourcedInitialStateFactory<TState, TFactory>(
        this IServiceCollection services)
        where TFactory : class, IInitialStateFactory<TState>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IInitialStateFactory<TState>, TFactory>();

        return services;
    }

    /// <summary>
    /// Registers the factory that creates the initial state for a state type.
    /// </summary>
    public static IServiceCollection AddEventSourcedInitialStateFactory<TState>(
        this IServiceCollection services,
        IInitialStateFactory<TState> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.AddSingleton(factory);

        return services;
    }

    /// <summary>
    /// Registers the factory that creates the initial state for a state type.
    /// </summary>
    public static IServiceCollection AddEventSourcedInitialStateFactory<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, CancellationToken, ValueTask<TState>> factory)
    {
        return services.AddEventSourcedInitialState(factory);
    }
}
