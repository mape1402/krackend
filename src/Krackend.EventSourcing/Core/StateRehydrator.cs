using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Diagnostics;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Snapshots;
using Krackend.EventSourcing.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Core;

/// <summary>
/// Default state rehydrator.
/// </summary>
public sealed class StateRehydrator : IStateRehydrator
{
    private readonly IEventStore _eventStore;
    private readonly IEventSerializer _serializer;
    private readonly IEventTypeRegistry _eventTypeRegistry;
    private readonly IStateSchemaRegistry _stateSchemaRegistry;
    private readonly IEventReducerRegistry _reducers;
    private readonly ISnapshotStore? _snapshotStore;
    private readonly ISnapshotSerializer? _snapshotSerializer;
    private readonly IServiceProvider? _serviceProvider;
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
        EventSourcingOptions? options = null,
        IServiceProvider? serviceProvider = null)
        : this(
            eventStore,
            serializer,
            eventTypeRegistry,
            new StateSchemaRegistry(),
            reducers,
            snapshotStore,
            snapshotSerializer,
            options,
            serviceProvider)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateRehydrator"/> class.
    /// </summary>
    public StateRehydrator(
        IEventStore eventStore,
        IEventSerializer serializer,
        IEventTypeRegistry eventTypeRegistry,
        IStateSchemaRegistry? stateSchemaRegistry,
        IEventReducerRegistry reducers,
        ISnapshotStore? snapshotStore = null,
        ISnapshotSerializer? snapshotSerializer = null,
        EventSourcingOptions? options = null,
        IServiceProvider? serviceProvider = null)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _eventTypeRegistry = eventTypeRegistry ?? throw new ArgumentNullException(nameof(eventTypeRegistry));
        _stateSchemaRegistry = stateSchemaRegistry ?? new StateSchemaRegistry();
        _reducers = reducers ?? throw new ArgumentNullException(nameof(reducers));
        _snapshotStore = snapshotStore;
        _snapshotSerializer = snapshotSerializer;
        _serviceProvider = serviceProvider;
        _batchSize = options?.RehydrationBatchSize ?? 500;

        if (_batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Rehydration batch size must be greater than zero.");
    }

    /// <inheritdoc />
    public async Task<EventSourcedState<TState>> RehydrateAsync<TState>(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default)
    {
        if (_serviceProvider is null)
            throw new InitialStateNotConfiguredException(typeof(TState));

        var initialState = await _serviceProvider
            .GetRequiredService<IInitialStateFactory<TState>>()
            .CreateAsync(cancellationToken);

        return await RehydrateAsync(streamName, streamId, initialState, cancellationToken);
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
                var eventClrType = _eventTypeRegistry.Resolve(envelope.EventType, envelope.EventSchemaVersion);

                var @event = _serializer.Deserialize(envelope.Payload, eventClrType)
                    ?? throw new EventPayloadDeserializationException(envelope.EventType, envelope.EventSchemaVersion, eventClrType);

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

        var stateSchema = GetStateSchemaRegistration(typeof(TState));

        if (snapshot.StateType != stateSchema.StateType || snapshot.StateSchemaVersion != stateSchema.StateSchemaVersion)
            throw new SnapshotStateSchemaMismatchException(
                snapshot.StateType,
                snapshot.StateSchemaVersion,
                stateSchema.StateType,
                stateSchema.StateSchemaVersion);

        if (_snapshotSerializer is null)
            throw new SnapshotSerializerMissingException();

        return (TState)(_snapshotSerializer.Deserialize(snapshot.Payload, typeof(TState))
            ?? throw new SnapshotDeserializationException(snapshot.StreamName, snapshot.StreamId, typeof(TState)));
    }

    private StateSchemaRegistration GetStateSchemaRegistration(Type stateType)
    {
        try
        {
            return _stateSchemaRegistry.GetRegistration(stateType);
        }
        catch (StateTypeNotRegisteredException) when (_stateSchemaRegistry is StateSchemaRegistry registry)
        {
            return registry.Register(stateType).GetRegistration(stateType);
        }
    }
}
