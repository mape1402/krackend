using Krackend.EventSourcing.Envelopes;

namespace Krackend.EventSourcing.Stores;

/// <summary>
/// In-memory event store implementation for tests and local development.
/// </summary>
public sealed class InMemoryEventStore : IEventStore
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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var key = new StreamKey(streamName, streamId);

            if (!_streams.TryGetValue(key, out var events))
                return Task.FromResult<IReadOnlyCollection<EventEnvelope>>([]);

            return Task.FromResult<IReadOnlyCollection<EventEnvelope>>(events.ToArray());
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

    private readonly record struct StreamKey(string StreamName, string StreamId);
}
