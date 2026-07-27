using Krackend.EventSourcing.Envelopes;

namespace Krackend.EventSourcing.Stores;

/// <summary>
/// In-memory event store implementation for tests and local development.
/// </summary>
public sealed class InMemoryEventStore : IEventStore, IEventLogReader
{
    private readonly IEventEnvelopeFactory _envelopeFactory;
    private readonly object _syncRoot = new();
    private readonly Dictionary<StreamKey, List<EventEnvelope>> _streams = [];
    private long _globalPosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryEventStore"/> class.
    /// </summary>
    public InMemoryEventStore(IEventEnvelopeFactory envelopeFactory)
    {
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventEnvelope>> LoadAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default)
        => ReadStreamAsync(streamName, streamId, 1, int.MaxValue, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventEnvelope>> ReadStreamAsync(
        string streamName,
        string streamId,
        long fromVersion,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        if (fromVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(fromVersion), "From version must be greater than zero.");

        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be greater than zero.");

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var key = new StreamKey(streamName, streamId);

            if (!_streams.TryGetValue(key, out var events))
                return Task.FromResult<IReadOnlyCollection<EventEnvelope>>([]);

            return Task.FromResult<IReadOnlyCollection<EventEnvelope>>(
                events
                    .Where(envelope => envelope.StreamVersion >= fromVersion)
                    .OrderBy(envelope => envelope.StreamVersion)
                    .Take(maxCount)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<long> GetCurrentVersionAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult(
                _streams.TryGetValue(new StreamKey(streamName, streamId), out var stream) && stream.Count > 0
                    ? stream[^1].StreamVersion
                    : 0);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
        string streamName,
        string streamId,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(events);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var key = new StreamKey(streamName, streamId);

            if (!_streams.TryGetValue(key, out var stream))
            {
                stream = [];
                _streams[key] = stream;
            }

            return AppendCore(streamName, streamId, stream, stream.Count == 0 ? 0 : stream[^1].StreamVersion, events);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
        string streamName,
        string streamId,
        long expectedVersion,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(events);

        if (expectedVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected version cannot be negative.");

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var key = new StreamKey(streamName, streamId);

            if (!_streams.TryGetValue(key, out var stream))
            {
                stream = [];
                _streams[key] = stream;
            }

            var actualVersion = stream.Count == 0 ? 0 : stream[^1].StreamVersion;

            if (actualVersion != expectedVersion)
                throw new EventStoreConcurrencyException(streamName, streamId, expectedVersion, actualVersion);

            return AppendCore(streamName, streamId, stream, expectedVersion, events);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<EventEnvelope>> ReadFromAsync(
        string streamName,
        long afterGlobalPosition,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        if (afterGlobalPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(afterGlobalPosition), "Global position cannot be negative.");

        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be greater than zero.");

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var events = _streams
                .Where(pair => pair.Key.StreamName == streamName)
                .SelectMany(pair => pair.Value)
                .Where(envelope => envelope.GlobalPosition > afterGlobalPosition)
                .OrderBy(envelope => envelope.GlobalPosition)
                .Take(maxCount)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<EventEnvelope>>(events);
        }
    }

    private readonly record struct StreamKey(string StreamName, string StreamId);

    private Task<IReadOnlyCollection<EventEnvelope>> AppendCore(
        string streamName,
        string streamId,
        List<EventEnvelope> stream,
        long expectedVersion,
        IReadOnlyCollection<object> events)
    {
        var envelopes = _envelopeFactory.Create(
                streamName,
                streamId,
                streamType: null,
                expectedVersion,
                events)
            .Select(envelope => envelope with { GlobalPosition = ++_globalPosition })
            .ToArray();

        stream.AddRange(envelopes);

        return Task.FromResult<IReadOnlyCollection<EventEnvelope>>(envelopes);
    }
}
