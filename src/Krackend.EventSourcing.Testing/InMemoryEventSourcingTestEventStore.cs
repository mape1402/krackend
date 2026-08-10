using System.Text.Json;
using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Streams;

namespace Krackend.EventSourcing.Testing;

/// <summary>
/// In-memory implementation of <see cref="IEventSourcingTestEventStore"/>.
/// </summary>
public sealed class InMemoryEventSourcingTestEventStore : IEventSourcingTestEventStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<EventStreamReference, List<EventSourcingTestEventEnvelope>> _streams = [];
    private readonly HashSet<EventStreamReference> _failedAppends = [];
    private long _globalPosition;

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventSourcingTestEventEnvelope>> AppendAsync(
        EventStreamReference stream,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default)
        => AppendAsync(stream, ExpectedVersion.Any, events, EmptyMetadata(), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventSourcingTestEventEnvelope>> AppendAsync(
        EventStreamReference stream,
        IReadOnlyCollection<object> events,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken = default)
        => AppendAsync(stream, ExpectedVersion.Any, events, metadata, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventSourcingTestEventEnvelope>> AppendAsync(
        EventStreamReference stream,
        ExpectedVersion expectedVersion,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default)
        => AppendAsync(stream, expectedVersion, events, EmptyMetadata(), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventSourcingTestEventEnvelope>> AppendAsync(
        EventStreamReference stream,
        ExpectedVersion expectedVersion,
        IReadOnlyCollection<object> events,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken = default)
    {
        ValidateStream(stream);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(metadata);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_streams.TryGetValue(stream, out var storedEvents))
            {
                storedEvents = [];
                _streams[stream] = storedEvents;
            }

            var actualVersion = storedEvents.Count == 0 ? 0 : storedEvents[^1].StreamVersion;

            if (_failedAppends.Remove(stream))
                throw new EventStoreConcurrencyException(stream.Name, stream.Id, actualVersion, actualVersion + 1);

            EnsureExpectedVersion(stream, expectedVersion, actualVersion);

            var appended = new List<EventSourcingTestEventEnvelope>(events.Count);
            var version = actualVersion;
            var occurredAt = DateTimeOffset.UtcNow;
            var eventMetadata = new Dictionary<string, object?>(metadata, StringComparer.Ordinal);

            foreach (var @event in events)
            {
                ArgumentNullException.ThrowIfNull(@event);

                version++;
                _globalPosition++;

                var envelope = new EventSourcingTestEventEnvelope(
                    EventId: Guid.NewGuid(),
                    Stream: stream,
                    StreamVersion: version,
                    GlobalPosition: _globalPosition,
                    EventType: @event.GetType().Name,
                    EventClrType: @event.GetType(),
                    Event: @event,
                    SerializedPayload: JsonSerializer.Serialize(@event, @event.GetType()),
                    Metadata: eventMetadata,
                    OccurredAt: occurredAt);

                storedEvents.Add(envelope);
                appended.Add(envelope);
            }

            return Task.FromResult<IReadOnlyCollection<EventSourcingTestEventEnvelope>>(appended);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventSourcingTestEventEnvelope>> ReadAsync(
        EventStreamReference stream,
        CancellationToken cancellationToken = default)
    {
        ValidateStream(stream);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyCollection<EventSourcingTestEventEnvelope>>(
                _streams.TryGetValue(stream, out var events)
                    ? events.OrderBy(x => x.StreamVersion).ToArray()
                    : []);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventSourcingTestEventEnvelope>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyCollection<EventSourcingTestEventEnvelope>>(
                _streams
                    .SelectMany(x => x.Value)
                    .OrderBy(x => x.GlobalPosition)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public void FailNextAppendWithConcurrencyConflict(EventStreamReference stream)
    {
        ValidateStream(stream);

        lock (_syncRoot)
        {
            _failedAppends.Add(stream);
        }
    }

    private static IReadOnlyDictionary<string, object?> EmptyMetadata()
        => new Dictionary<string, object?>(StringComparer.Ordinal);

    private static void EnsureExpectedVersion(
        EventStreamReference stream,
        ExpectedVersion expectedVersion,
        long actualVersion)
    {
        if (expectedVersion.Mode == ExpectedVersionMode.Any)
            return;

        var expected = expectedVersion.Mode == ExpectedVersionMode.NoStream
            ? 0
            : expectedVersion.Value;

        if (actualVersion != expected)
            throw new EventStoreConcurrencyException(stream.Name, stream.Id, expected, actualVersion);
    }

    private static void ValidateStream(EventStreamReference stream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stream.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(stream.Id);
    }
}
