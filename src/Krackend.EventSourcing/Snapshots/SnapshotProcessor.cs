using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Diagnostics;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Stores;

namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Builds snapshots for pending stream candidates.
/// </summary>
public sealed class SnapshotProcessor<TState> : ISnapshotProcessor<TState>
{
    private readonly IEventStore _eventStore;
    private readonly IEventSerializer _eventSerializer;
    private readonly IEventTypeRegistry _eventTypeRegistry;
    private readonly IEventReducerRegistry _reducers;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ISnapshotSerializer _snapshotSerializer;
    private readonly ISnapshotCandidateStore _candidateStore;
    private readonly IInitialStateFactory<TState> _initialStateFactory;
    private readonly int _batchSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotProcessor{TState}"/> class.
    /// </summary>
    public SnapshotProcessor(
        IEventStore eventStore,
        IEventSerializer eventSerializer,
        IEventTypeRegistry eventTypeRegistry,
        IEventReducerRegistry reducers,
        ISnapshotStore snapshotStore,
        ISnapshotSerializer snapshotSerializer,
        ISnapshotCandidateStore candidateStore,
        EventSourcingOptions options,
        IInitialStateFactory<TState> initialStateFactory)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _eventTypeRegistry = eventTypeRegistry ?? throw new ArgumentNullException(nameof(eventTypeRegistry));
        _reducers = reducers ?? throw new ArgumentNullException(nameof(reducers));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _snapshotSerializer = snapshotSerializer ?? throw new ArgumentNullException(nameof(snapshotSerializer));
        _candidateStore = candidateStore ?? throw new ArgumentNullException(nameof(candidateStore));
        _initialStateFactory = initialStateFactory ?? throw new ArgumentNullException(nameof(initialStateFactory));

        ArgumentNullException.ThrowIfNull(options);
        _batchSize = options.RehydrationBatchSize;

        if (_batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Snapshot batch size must be greater than zero.");
    }

    /// <inheritdoc />
    public async Task<SnapshotProcessingResult> ProcessAsync(
        SnapshotCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        var initialState = await _initialStateFactory.CreateAsync(cancellationToken);

        return await ProcessAsync(candidate, initialState, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SnapshotProcessingResult> ProcessAsync(
        SnapshotCandidate candidate,
        TState initialState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var latestSnapshot = await _snapshotStore.LoadLatestAsync(
            candidate.StreamName,
            candidate.StreamId,
            cancellationToken);

        if (latestSnapshot is not null && latestSnapshot.StreamVersion >= candidate.StreamVersion)
        {
            await _candidateStore.CompleteAsync(candidate, cancellationToken);
            return new SnapshotProcessingResult(
                candidate.StreamName,
                candidate.StreamId,
                latestSnapshot.StreamVersion,
                SnapshotSaved: false);
        }

        var state = LoadSnapshotState(latestSnapshot, initialState);
        var version = latestSnapshot?.StreamVersion ?? 0;
        var nextVersion = version + 1;

        while (version < candidate.StreamVersion)
        {
            var envelopes = await _eventStore.ReadStreamAsync(
                candidate.StreamName,
                candidate.StreamId,
                nextVersion,
                _batchSize,
                cancellationToken);

            if (envelopes.Count == 0)
                break;

            foreach (var envelope in envelopes.OrderBy(x => x.StreamVersion))
            {
                if (envelope.StreamVersion > candidate.StreamVersion)
                    break;

                var eventClrType = _eventTypeRegistry.Resolve(envelope.EventType, envelope.EventSchemaVersion);

                var @event = _eventSerializer.Deserialize(envelope.Payload, eventClrType)
                    ?? throw new EventPayloadDeserializationException(envelope.EventType, envelope.EventSchemaVersion, eventClrType);

                state = _reducers.Apply(state, @event);
                version = envelope.StreamVersion;
                nextVersion = version + 1;
            }

            if (envelopes.Count < _batchSize)
                break;
        }

        if (version <= (latestSnapshot?.StreamVersion ?? 0))
        {
            await _candidateStore.CompleteAsync(candidate, cancellationToken);
            return new SnapshotProcessingResult(
                candidate.StreamName,
                candidate.StreamId,
                version,
                SnapshotSaved: false);
        }

        var stateSchema = StateSchemaResolver.Resolve(typeof(TState));

        await _snapshotStore.SaveAsync(new Snapshot(
            candidate.StreamName,
            candidate.StreamId,
            version,
            stateSchema.StateType,
            stateSchema.StateSchemaVersion,
            _snapshotSerializer.Serialize(state),
            DateTimeOffset.UtcNow), cancellationToken);

        await _candidateStore.CompleteAsync(candidate, cancellationToken);

        return new SnapshotProcessingResult(
            candidate.StreamName,
            candidate.StreamId,
            version,
            SnapshotSaved: true);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<SnapshotProcessingResult>> ProcessPendingAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var initialState = await _initialStateFactory.CreateAsync(cancellationToken);

        return await ProcessPendingAsync(initialState, maxCount, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<SnapshotProcessingResult>> ProcessPendingAsync(
        TState initialState,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be greater than zero.");

        var candidates = await _candidateStore.GetPendingAsync(maxCount, cancellationToken);
        var results = new List<SnapshotProcessingResult>(candidates.Count);

        foreach (var candidate in candidates)
        {
            results.Add(await ProcessAsync(candidate, initialState, cancellationToken));
        }

        return results;
    }

    private TState LoadSnapshotState(Snapshot? snapshot, TState initialState)
    {
        if (snapshot is null)
            return initialState;

        var stateSchema = StateSchemaResolver.Resolve(typeof(TState));

        if (snapshot.StateType != stateSchema.StateType || snapshot.StateSchemaVersion != stateSchema.StateSchemaVersion)
            throw new SnapshotStateSchemaMismatchException(
                snapshot.StateType,
                snapshot.StateSchemaVersion,
                stateSchema.StateType,
                stateSchema.StateSchemaVersion);

        return (TState)(_snapshotSerializer.Deserialize(snapshot.Payload, typeof(TState))
            ?? throw new SnapshotDeserializationException(snapshot.StreamName, snapshot.StreamId, typeof(TState)));
    }
}
