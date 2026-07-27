using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Snapshots;
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
    private readonly ISnapshotStore? _snapshotStore;
    private readonly ISnapshotSerializer? _snapshotSerializer;
    private readonly int _batchSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateRehydrator"/> class.
    /// </summary>
    public StateRehydrator(
        IEventStore eventStore,
        IEventSerializer serializer,
        IEventTypeRegistry eventTypeRegistry,
        IEventReducerRegistry reducers,
        ISnapshotStore? snapshotStore = null,
        ISnapshotSerializer? snapshotSerializer = null,
        EventSourcingOptions? options = null)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _eventTypeRegistry = eventTypeRegistry ?? throw new ArgumentNullException(nameof(eventTypeRegistry));
        _reducers = reducers ?? throw new ArgumentNullException(nameof(reducers));
        _snapshotStore = snapshotStore;
        _snapshotSerializer = snapshotSerializer;
        _batchSize = options?.RehydrationBatchSize ?? 500;

        if (_batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Rehydration batch size must be greater than zero.");
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

        var snapshot = _snapshotStore is null
            ? null
            : await _snapshotStore.LoadLatestAsync(streamName, streamId, cancellationToken);

        var state = LoadSnapshotState(snapshot, initialState);
        var version = snapshot?.StreamVersion ?? 0L;
        var nextVersion = version + 1;

        while (true)
        {
            var envelopes = await _eventStore.ReadStreamAsync(
                streamName,
                streamId,
                nextVersion,
                _batchSize,
                cancellationToken);

            if (envelopes.Count == 0)
                break;

            foreach (var envelope in envelopes.OrderBy(x => x.StreamVersion))
            {
                var eventType = _eventTypeRegistry.Resolve(envelope.EventType, envelope.EventVersion);
                var @event = _serializer.Deserialize(envelope.Payload, eventType)
                    ?? throw new InvalidOperationException($"Event '{envelope.EventType}' could not be deserialized.");

                state = _reducers.Apply(state, @event);
                version = envelope.StreamVersion;
                nextVersion = version + 1;
            }

            if (envelopes.Count < _batchSize)
                break;
        }

        return new EventSourcedState<TState>(state, version);
    }

    private TState LoadSnapshotState<TState>(Snapshot? snapshot, TState initialState)
    {
        if (snapshot is null)
            return initialState;

        if (_snapshotSerializer is null)
            throw new InvalidOperationException("A snapshot serializer is required to load snapshots.");

        return (TState)(_snapshotSerializer.Deserialize(snapshot.Payload, typeof(TState))
            ?? throw new InvalidOperationException($"Snapshot for stream '{snapshot.StreamName}/{snapshot.StreamId}' could not be deserialized."));
    }
}
