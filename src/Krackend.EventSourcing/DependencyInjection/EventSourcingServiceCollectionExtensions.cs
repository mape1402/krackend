using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Outbox;
using Krackend.EventSourcing.Projections;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Streams;
using Krackend.EventSourcing.Upcasting;
using Microsoft.Extensions.DependencyInjection;

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
        options.Stores.Add(options.Routing.DefaultStreamName);
        configure?.Invoke(options);

        var eventTypeRegistry = new EventTypeRegistry();

        services.AddSingleton(options);
        services.AddSingleton(options.Envelope);
        services.AddSingleton(options.Routing);
        services.AddSingleton(options.Stores);
        services.AddSingleton<IEventTypeRegistry>(eventTypeRegistry);
        services.AddSingleton(eventTypeRegistry);
        services.AddSingleton<IEventSerializer, SystemTextJsonEventSerializer>();
        services.AddSingleton<IEventStreamResolver, ConfiguredEventStreamResolver>();
        services.AddSingleton<IEventUpcasterPipeline>(provider => new EventUpcasterPipeline(provider.GetServices<IEventUpcaster>()));
        services.AddSingleton<ICheckpointStore, InMemoryCheckpointStore>();
        services.AddSingleton<IOutboxStore, InMemoryOutboxStore>();
        services.AddScoped<EventMetadataCollector>();
        services.AddScoped<IEventEnvelopeFactory, EventEnvelopeFactory>();
        services.AddScoped<InMemoryEventStore>();
        services.AddScoped<IEventStore>(provider => provider.GetRequiredService<InMemoryEventStore>());
        services.AddScoped<IEventLogReader>(provider => provider.GetRequiredService<InMemoryEventStore>());
        services.AddScoped<IProjectionRunner, ProjectionRunner>();

        return services;
    }
}
