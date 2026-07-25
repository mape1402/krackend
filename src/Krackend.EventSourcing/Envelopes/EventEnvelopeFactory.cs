using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;

namespace Krackend.EventSourcing.Envelopes;

/// <summary>
/// Default event envelope factory.
/// </summary>
public sealed class EventEnvelopeFactory : IEventEnvelopeFactory
{
    private readonly IEventTypeRegistry _eventTypeRegistry;
    private readonly IEventSerializer _serializer;
    private readonly EventMetadataCollector _metadataCollector;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventEnvelopeFactory"/> class.
    /// </summary>
    public EventEnvelopeFactory(
        IEventTypeRegistry eventTypeRegistry,
        IEventSerializer serializer,
        EventMetadataCollector metadataCollector)
    {
        _eventTypeRegistry = eventTypeRegistry ?? throw new ArgumentNullException(nameof(eventTypeRegistry));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _metadataCollector = metadataCollector ?? throw new ArgumentNullException(nameof(metadataCollector));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<EventEnvelope> Create(
        string streamName,
        string streamId,
        string? streamType,
        long expectedVersion,
        IReadOnlyCollection<object> events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(events);

        var metadata = _metadataCollector.Collect();
        var serializedMetadata = metadata.Count == 0 ? null : _serializer.Serialize(metadata);
        var occurredAt = DateTimeOffset.UtcNow;
        var envelopes = new List<EventEnvelope>(events.Count);
        var version = expectedVersion;

        foreach (var @event in events)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var registration = _eventTypeRegistry.GetRegistration(@event.GetType());
            version++;

            envelopes.Add(new EventEnvelope(
                EventId: Guid.NewGuid(),
                StreamName: streamName,
                StreamId: streamId,
                StreamType: streamType,
                StreamVersion: version,
                GlobalPosition: null,
                EventType: registration.EventType,
                EventVersion: registration.EventVersion,
                OccurredAt: occurredAt,
                Payload: _serializer.Serialize(@event),
                Metadata: serializedMetadata));
        }

        return envelopes;
    }
}
