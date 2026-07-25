using Krackend.EventSourcing.Aggregates;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Stores;

namespace Krackend.EventSourcing.Repositories;

/// <summary>
/// Default event sourced aggregate repository.
/// </summary>
public sealed class EventSourcedRepository<TAggregate> : IEventSourcedRepository<TAggregate>
    where TAggregate : IAggregateRoot, new()
{
    private readonly IEventStore _eventStore;
    private readonly IEventSerializer _serializer;
    private readonly IEventTypeRegistry _eventTypeRegistry;
    private readonly string _streamName;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcedRepository{TAggregate}"/> class.
    /// </summary>
    public EventSourcedRepository(
        IEventStore eventStore,
        IEventSerializer serializer,
        IEventTypeRegistry eventTypeRegistry,
        string streamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _eventTypeRegistry = eventTypeRegistry ?? throw new ArgumentNullException(nameof(eventTypeRegistry));
        _streamName = streamName;
    }

    /// <inheritdoc />
    public async Task<TAggregate> LoadAsync(string streamId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        var envelopes = await _eventStore.LoadAsync(_streamName, streamId, cancellationToken);
        var events = envelopes
            .OrderBy(envelope => envelope.StreamVersion)
            .Select(Deserialize)
            .ToArray();

        var aggregate = new TAggregate();
        aggregate.LoadFromHistory(events);

        return aggregate;
    }

    /// <inheritdoc />
    public async Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        if (aggregate.PendingEvents.Count == 0)
            return;

        var expectedVersion = aggregate.Version - aggregate.PendingEvents.Count;

        await _eventStore.AppendAsync(
            _streamName,
            aggregate.Id,
            expectedVersion,
            aggregate.PendingEvents,
            cancellationToken);

        aggregate.ClearPendingEvents();
    }

    private object Deserialize(Envelopes.EventEnvelope envelope)
    {
        var eventType = _eventTypeRegistry.Resolve(envelope.EventType, envelope.EventVersion);
        var @event = _serializer.Deserialize(envelope.Payload, eventType);

        return @event ?? throw new InvalidOperationException(
            $"Event '{envelope.EventType}' version '{envelope.EventVersion}' could not be deserialized.");
    }
}
