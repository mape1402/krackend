using Krackend.EventSourcing.Snapshots;
using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Streams;

namespace Krackend.EventSourcing.Core;

/// <summary>
/// Default event sourced application service.
/// </summary>
public sealed class EventSourcedApplicationService<TState, TCommand> : IEventSourcedApplicationService<TState, TCommand>
{
    private readonly IStateRehydrator _rehydrator;
    private readonly IEventDecider<TState, TCommand> _decider;
    private readonly IEventReducerRegistry _reducers;
    private readonly IEventStore _eventStore;
    private readonly ICommandStreamResolver<TCommand> _streamResolver;
    private readonly ISnapshotStore? _snapshotStore;
    private readonly ISnapshotSerializer? _snapshotSerializer;
    private readonly ISnapshotStrategy? _snapshotStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcedApplicationService{TState, TCommand}"/> class.
    /// </summary>
    public EventSourcedApplicationService(
        IStateRehydrator rehydrator,
        IEventDecider<TState, TCommand> decider,
        IEventReducerRegistry reducers,
        IEventStore eventStore,
        ICommandStreamResolver<TCommand> streamResolver,
        ISnapshotStore? snapshotStore = null,
        ISnapshotSerializer? snapshotSerializer = null,
        ISnapshotStrategy? snapshotStrategy = null)
    {
        _rehydrator = rehydrator ?? throw new ArgumentNullException(nameof(rehydrator));
        _decider = decider ?? throw new ArgumentNullException(nameof(decider));
        _reducers = reducers ?? throw new ArgumentNullException(nameof(reducers));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _streamResolver = streamResolver ?? throw new ArgumentNullException(nameof(streamResolver));
        _snapshotStore = snapshotStore;
        _snapshotSerializer = snapshotSerializer;
        _snapshotStrategy = snapshotStrategy;
    }

    /// <inheritdoc />
    public Task<EventSourcingExecutionResult<TState>> ExecuteAsync(
        TState initialState,
        TCommand command,
        CancellationToken cancellationToken = default)
    {
        var stream = _streamResolver.Resolve(command);
        return ExecuteAsync(stream.Name, stream.Id, initialState, command, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EventSourcingExecutionResult<TState>> ExecuteAsync(
        string streamName,
        string streamId,
        TState initialState,
        TCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(command);

        var rehydrated = await _rehydrator.RehydrateAsync(streamName, streamId, initialState, cancellationToken);
        var events = await _decider.DecideAsync(rehydrated.State, command, cancellationToken);
        var committedEvents = await _eventStore.AppendAsync(streamName, streamId, rehydrated.Version, events, cancellationToken);
        var currentState = rehydrated.State;

        foreach (var @event in events)
        {
            currentState = _reducers.Apply(currentState, @event);
        }

        if (_snapshotStore is not null
            && _snapshotSerializer is not null
            && _snapshotStrategy?.ShouldCreateSnapshot(streamName, streamId, rehydrated.Version + events.Count) == true)
        {
            await _snapshotStore.SaveAsync(new Snapshot(
                streamName,
                streamId,
                rehydrated.Version + events.Count,
                _snapshotSerializer.Serialize(currentState),
                DateTimeOffset.UtcNow), cancellationToken);
        }

        return new EventSourcingExecutionResult<TState>(
            PreviousState: rehydrated.State,
            CurrentState: currentState,
            PreviousVersion: rehydrated.Version,
            CurrentVersion: rehydrated.Version + events.Count,
            CommittedEvents: committedEvents);
    }
}
