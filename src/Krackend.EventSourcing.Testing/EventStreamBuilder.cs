using Krackend.EventSourcing.Contracts;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;

namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Builds event envelopes for tests.
/// </summary>
public sealed class EventStreamBuilder
{
    private readonly List<object> _events = [];
    private readonly string _streamName;
    private readonly string _streamId;
    private readonly IEventSerializer _serializer;
    private readonly EventTypeRegistry _eventTypes;

    private long _globalPosition;
    private long _startVersion = 1;

    private EventStreamBuilder(
        string streamName,
        string streamId,
        IEventSerializer serializer,
        EventTypeRegistry eventTypes)
    {
        _streamName = streamName;
        _streamId = streamId;
        _serializer = serializer;
        _eventTypes = eventTypes;
    }

    /// <summary>
    /// Creates a builder for a stream.
    /// </summary>
    public static EventStreamBuilder ForStream(string streamName, string streamId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        return new EventStreamBuilder(
            streamName,
            streamId,
            new SystemTextJsonEventSerializer(),
            new EventTypeRegistry());
    }

    /// <summary>
    /// Sets the first stream version to emit.
    /// </summary>
    public EventStreamBuilder StartingAt(long streamVersion)
    {
        if (streamVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(streamVersion), "Stream version must be greater than zero.");

        _startVersion = streamVersion;
        return this;
    }

    /// <summary>
    /// Registers an event type for envelope schema metadata.
    /// </summary>
    public EventStreamBuilder Register<TEvent>(string? eventType = null, SemanticVersion eventSchemaVersion = default)
    {
        _eventTypes.Register<TEvent>(eventType, eventSchemaVersion);
        return this;
    }

    /// <summary>
    /// Adds an event to the stream.
    /// </summary>
    public EventStreamBuilder Add(object @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        _events.Add(@event);
        return this;
    }

    /// <summary>
    /// Builds the event envelopes.
    /// </summary>
    public IReadOnlyCollection<EventEnvelope> Build()
    {
        var envelopes = new List<EventEnvelope>(_events.Count);
        var streamVersion = _startVersion;

        foreach (var @event in _events)
        {
            var registration = _eventTypes.GetRegistration(@event.GetType());

            envelopes.Add(new EventEnvelope(
                EventId: Guid.NewGuid(),
                StreamName: _streamName,
                StreamId: _streamId,
                StreamType: null,
                StreamVersion: streamVersion++,
                GlobalPosition: ++_globalPosition,
                EventType: registration.EventType,
                EventSchemaVersion: registration.EventSchemaVersion,
                OccurredAt: DateTimeOffset.UtcNow,
                CorrelationId: null,
                CausationId: null,
                UserId: null,
                TenantId: null,
                Source: "test",
                Payload: _serializer.Serialize(@event),
                Metadata: null));
        }

        return envelopes;
    }
}
