using Krackend.EventSourcing.Snapshots;
using Microsoft.EntityFrameworkCore;

namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// EF Core snapshot candidate store using the application's DbContext.
/// </summary>
public sealed class EntityFrameworkSnapshotCandidateStore<TDbContext> : ISnapshotCandidateStore
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkSnapshotCandidateStore{TDbContext}"/> class.
    /// </summary>
    public EntityFrameworkSnapshotCandidateStore(IEventStoreDbContextFactory<TDbContext> dbContextFactory)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        _dbContext = dbContextFactory.CreateDbContext();
    }

    /// <inheritdoc />
    public async Task MarkAsync(
        string streamName,
        string streamId,
        long streamVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        if (streamVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(streamVersion), "Stream version must be greater than zero.");

        var set = _dbContext.Set<SnapshotCandidateRecord>();
        var record = await set.FindAsync([streamName, streamId], cancellationToken);

        if (record is null)
        {
            set.Add(new SnapshotCandidateRecord
            {
                StreamName = streamName,
                StreamId = streamId,
                StreamVersion = streamVersion,
                MarkedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            record.StreamVersion = Math.Max(record.StreamVersion, streamVersion);
            record.MarkedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<SnapshotCandidate>> GetPendingAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be greater than zero.");

        var records = await _dbContext.Set<SnapshotCandidateRecord>()
            .AsNoTracking()
            .OrderBy(x => x.StreamName)
            .ThenBy(x => x.StreamId)
            .ThenBy(x => x.StreamVersion)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        return records
            .Select(record => new SnapshotCandidate(
                record.StreamName,
                record.StreamId,
                record.StreamVersion,
                record.MarkedAt))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task CompleteAsync(
        SnapshotCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var set = _dbContext.Set<SnapshotCandidateRecord>();
        var record = await set.FindAsync([candidate.StreamName, candidate.StreamId], cancellationToken);

        if (record is null)
            return;

        set.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
