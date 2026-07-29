using Krackend.EventSourcing.Snapshots;
using Krackend.EventSourcing.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// EF Core snapshot store using the application's DbContext.
/// </summary>
public sealed class EntityFrameworkSnapshotStore<TDbContext> : ISnapshotStore
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkSnapshotStore{TDbContext}"/> class.
    /// </summary>
    public EntityFrameworkSnapshotStore(IEventStoreDbContextFactory<TDbContext> dbContextFactory)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        _dbContext = dbContextFactory.CreateDbContext();
    }

    /// <inheritdoc />
    public async Task<Snapshot?> LoadLatestAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        var record = await _dbContext.Set<EventSnapshotRecord>()
            .AsNoTracking()
            .Where(x => x.StreamName == streamName && x.StreamId == streamId)
            .OrderByDescending(x => x.StreamVersion)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null
            ? null
            : new Snapshot(
                record.StreamName,
                record.StreamId,
                record.StreamVersion,
                record.StateType,
                SemanticVersion.Parse(record.StateSchemaVersion),
                record.Payload,
                record.CreatedAt);
    }

    /// <inheritdoc />
    public async Task SaveAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _dbContext.Set<EventSnapshotRecord>().Add(new EventSnapshotRecord
        {
            SnapshotId = Guid.NewGuid(),
            StreamName = snapshot.StreamName,
            StreamId = snapshot.StreamId,
            StreamVersion = snapshot.StreamVersion,
            StateType = snapshot.StateType,
            StateSchemaVersion = snapshot.StateSchemaVersion.ToString(),
            Payload = snapshot.Payload,
            CreatedAt = snapshot.CreatedAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
