using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Stores;

namespace Krackend.EventSourcing.Core;

/// <summary>
/// Default state rehydrator.
/// </summary>
public sealed class StateRehydrator : IStateRehydrator
{
    private readonly IEventStore _eventStore;
    private readonly IEventSerializer _serializer;
    private readonly IEventTypeRegistry _eventTypeRegistry;
    private readonly IEventReducerRegistry _reducers;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateRehydrator"/> class.
    /// </summary>
    public StateRehydrator(
        IEventStore eventStore,
        IEventSerializer serializer,
        IEventTypeRegistry eventTypeRegistry,
        IEventReducerRegistry reducers)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _eventTypeRegistry = eventTypeRegistry ?? throw new ArgumentNullException(nameof(eventTypeRegistry));
        _reducers = reducers ?? throw new ArgumentNullException(nameof(reducers));
    }

    /// <inheritdoc />
    public async Task<EventSourcedState<TState>> RehydrateAsync<TState>(
        string streamName,
        string streamId,
        TState initialState,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        var envelopes = await _eventStore.LoadAsync(streamName, streamId, cancellationToken);
        var state = initialState;
        var version = 0L;

        foreach (var envelope in envelopes.OrderBy(x => x.StreamVersion))
        {
            var eventType = _eventTypeRegistry.Resolve(envelope.EventType, envelope.EventVersion);
            var @event = _serializer.Deserialize(envelope.Payload, eventType)
                ?? throw new InvalidOperationException($"Event '{envelope.EventType}' could not be deserialized.");

            state = _reducers.Apply(state, @event);
            version = envelope.StreamVersion;
        }

        return new EventSourcedState<TState>(state, version);
    }
}
