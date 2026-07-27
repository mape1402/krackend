using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Stores;
using Microsoft.EntityFrameworkCore;

namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core event store using entities added to the application DbContext model.
/// </summary>
public sealed class EntityFrameworkEventStore<TDbContext> : IEventStore, IEventLogReader, IDisposable, IAsyncDisposable
    where TDbContext : DbContext
{
    private readonly EventStoreOptionsCollection _stores;
    private readonly IEventEnvelopeFactory _envelopeFactory;
    private readonly TDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkEventStore{TDbContext}"/> class.
    /// </summary>
    public EntityFrameworkEventStore(
        EventStoreOptionsCollection stores,
        IEventEnvelopeFactory envelopeFactory,
        IEventStoreDbContextFactory<TDbContext> dbContextFactory)
    {
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));

        if (dbContextFactory is null)
            throw new ArgumentNullException(nameof(dbContextFactory));

        _dbContext = dbContextFactory.CreateDbContext();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventEnvelope>> LoadAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        EnsureStore(streamName);

        var records = await _dbContext.Set<EventStoreRecord>(streamName)
            .AsNoTracking()
            .Where(x => x.StreamName == streamName && x.StreamId == streamId)
            .OrderBy(x => x.StreamVersion)
            .ToListAsync(cancellationToken);

        return records.Select(ToEnvelope).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
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

        EnsureStore(streamName);

        var set = _dbContext.Set<EventStoreRecord>(streamName);
        var actualVersion = await set
            .Where(x => x.StreamName == streamName && x.StreamId == streamId)
            .MaxAsync(x => (long?)x.StreamVersion, cancellationToken) ?? 0;

        if (actualVersion != expectedVersion)
            throw new EventStoreConcurrencyException(streamName, streamId, expectedVersion, actualVersion);

        var nextGlobalPosition = await set.MaxAsync(x => (long?)x.GlobalPosition, cancellationToken) ?? 0;
        var pendingEnvelopes = _envelopeFactory.Create(streamName, streamId, null, expectedVersion, events);
        var committedEnvelopes = new List<EventEnvelope>(pendingEnvelopes.Count);

        foreach (var envelope in pendingEnvelopes)
        {
            nextGlobalPosition++;
            var committed = envelope with { GlobalPosition = nextGlobalPosition };
            set.Add(ToRecord(committed));
            committedEnvelopes.Add(committed);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return committedEnvelopes;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventEnvelope>> ReadFromAsync(
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

        EnsureStore(streamName);

        var records = await _dbContext.Set<EventStoreRecord>(streamName)
            .AsNoTracking()
            .Where(x => x.StreamName == streamName && x.GlobalPosition > afterGlobalPosition)
            .OrderBy(x => x.GlobalPosition)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        return records.Select(ToEnvelope).ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private void EnsureStore(string streamName)
        => _stores.GetRequired(streamName);

    private static EventStoreRecord ToRecord(EventEnvelope envelope)
        => new()
        {
            EventId = envelope.EventId,
            StreamName = envelope.StreamName,
            StreamId = envelope.StreamId,
            StreamType = envelope.StreamType,
            StreamVersion = envelope.StreamVersion,
            GlobalPosition = envelope.GlobalPosition ?? 0,
            EventType = envelope.EventType,
            EventVersion = envelope.EventVersion,
            OccurredAt = envelope.OccurredAt,
            Payload = envelope.Payload,
            Metadata = envelope.Metadata
        };

    private static EventEnvelope ToEnvelope(EventStoreRecord record)
        => new(
            EventId: record.EventId,
            StreamName: record.StreamName,
            StreamId: record.StreamId,
            StreamType: record.StreamType,
            StreamVersion: record.StreamVersion,
            GlobalPosition: record.GlobalPosition,
            EventType: record.EventType,
            EventVersion: record.EventVersion,
            OccurredAt: record.OccurredAt,
            Payload: record.Payload,
            Metadata: record.Metadata);
}
