using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Snapshots;
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
    private readonly ISnapshotCandidateMarker _snapshotCandidateMarker;
    private readonly TDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkEventStore{TDbContext}"/> class.
    /// </summary>
    public EntityFrameworkEventStore(
        EventStoreOptionsCollection stores,
        IEventEnvelopeFactory envelopeFactory,
        ISnapshotCandidateMarker snapshotCandidateMarker,
        IEventStoreDbContextFactory<TDbContext> dbContextFactory)
    {
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
        _snapshotCandidateMarker = snapshotCandidateMarker ?? throw new ArgumentNullException(nameof(snapshotCandidateMarker));

        if (dbContextFactory is null)
            throw new ArgumentNullException(nameof(dbContextFactory));

        _dbContext = dbContextFactory.CreateDbContext();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventEnvelope>> LoadAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default)
        => await ReadStreamAsync(streamName, streamId, 1, int.MaxValue, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventEnvelope>> ReadStreamAsync(
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

        EnsureStore(streamName);

        var records = await _dbContext.Set<EventStoreRecord>(streamName)
            .AsNoTracking()
            .Where(x => x.StreamName == streamName && x.StreamId == streamId && x.StreamVersion >= fromVersion)
            .OrderBy(x => x.StreamVersion)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        return records.Select(ToEnvelope).ToArray();
    }

    /// <inheritdoc />
    public async Task<long> GetCurrentVersionAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        EnsureStore(streamName);

        return await _dbContext.Set<EventStoreRecord>(streamName)
            .AsNoTracking()
            .Where(x => x.StreamName == streamName && x.StreamId == streamId)
            .MaxAsync(x => (long?)x.StreamVersion, cancellationToken) ?? 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
        string streamName,
        string streamId,
        long expectedVersion,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default)
        => await AppendAsync(streamName, streamId, ExpectedVersion.Exact(expectedVersion), events, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
        string streamName,
        string streamId,
        ExpectedVersion expectedVersion,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(events);

        var actualVersion = await GetCurrentVersionAsync(streamName, streamId, cancellationToken);

        EnsureExpectedVersion(streamName, streamId, expectedVersion, actualVersion);

        return await AppendCoreAsync(streamName, streamId, actualVersion, events, cancellationToken);
    }

    private async Task<IReadOnlyCollection<EventEnvelope>> AppendCoreAsync(
        string streamName,
        string streamId,
        long expectedVersion,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken)
    {
        EnsureStore(streamName);

        var set = _dbContext.Set<EventStoreRecord>(streamName);
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
        await _snapshotCandidateMarker.MarkIfNeededAsync(
            streamName,
            streamId,
            committedEnvelopes.Count == 0 ? expectedVersion : committedEnvelopes[^1].StreamVersion,
            cancellationToken);

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

    private static void EnsureExpectedVersion(
        string streamName,
        string streamId,
        ExpectedVersion expectedVersion,
        long actualVersion)
    {
        if (expectedVersion.Mode == ExpectedVersionMode.Any)
            return;

        var expected = expectedVersion.Mode == ExpectedVersionMode.NoStream
            ? 0
            : expectedVersion.Value;

        if (actualVersion != expected)
            throw new EventStoreConcurrencyException(streamName, streamId, expected, actualVersion);
    }

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
            EventSchemaVersion = envelope.EventSchemaVersion,
            OccurredAt = envelope.OccurredAt,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            UserId = envelope.UserId,
            TenantId = envelope.TenantId,
            Source = envelope.Source,
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
            EventSchemaVersion: record.EventSchemaVersion,
            OccurredAt: record.OccurredAt,
            CorrelationId: record.CorrelationId,
            CausationId: record.CausationId,
            UserId: record.UserId,
            TenantId: record.TenantId,
            Source: record.Source,
            Payload: record.Payload,
            Metadata: record.Metadata);
}
