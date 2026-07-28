using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Stores;

namespace Krackend.EventSourcing.Projections;

/// <summary>
/// Default projection runner.
/// </summary>
public sealed class ProjectionRunner : IProjectionRunner
{
    private readonly IEventLogReader _eventLogReader;
    private readonly ICheckpointStore _checkpointStore;
    private readonly IEventTypeRegistry _eventTypeRegistry;
    private readonly IEventSerializer _serializer;
    private readonly IReadOnlyDictionary<Type, IProjectionHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionRunner"/> class.
    /// </summary>
    public ProjectionRunner(
        IEventLogReader eventLogReader,
        ICheckpointStore checkpointStore,
        IEventTypeRegistry eventTypeRegistry,
        IEventSerializer serializer,
        IEnumerable<IProjectionHandler> handlers)
    {
        _eventLogReader = eventLogReader ?? throw new ArgumentNullException(nameof(eventLogReader));
        _checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
        _eventTypeRegistry = eventTypeRegistry ?? throw new ArgumentNullException(nameof(eventTypeRegistry));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _handlers = handlers?.ToDictionary(handler => handler.EventType)
            ?? throw new ArgumentNullException(nameof(handlers));
    }

    /// <inheritdoc />
    public async Task<int> RunBatchAsync(
        string projectionName,
        string streamName,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        var checkpoint = await _checkpointStore.GetLastPositionAsync(projectionName, streamName, cancellationToken);
        var envelopes = await _eventLogReader.ReadFromAsync(streamName, checkpoint, maxCount, cancellationToken);
        var processed = 0;

        foreach (var envelope in envelopes.OrderBy(envelope => envelope.GlobalPosition))
        {
            var eventType = _eventTypeRegistry.Resolve(envelope.EventType, envelope.EventSchemaVersion);
            var @event = _serializer.Deserialize(envelope.Payload, eventType)
                ?? throw new InvalidOperationException($"Event '{envelope.EventType}' could not be deserialized.");

            if (_handlers.TryGetValue(eventType, out var handler))
                await handler.HandleAsync(@event, cancellationToken);

            if (envelope.GlobalPosition.HasValue)
                await _checkpointStore.SaveAsync(projectionName, streamName, envelope.GlobalPosition.Value, cancellationToken);

            processed++;
        }

        return processed;
    }
}
