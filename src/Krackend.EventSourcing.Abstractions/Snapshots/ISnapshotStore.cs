namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Stores and loads aggregate snapshots.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// Loads the latest snapshot for a stream.
    /// </summary>
    Task<Snapshot?> LoadLatestAsync(string streamName, string streamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a snapshot.
    /// </summary>
    Task SaveAsync(Snapshot snapshot, CancellationToken cancellationToken = default);
}
