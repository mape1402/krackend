namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Determines when snapshots should be created.
/// </summary>
public interface ISnapshotStrategy
{
    /// <summary>
    /// Determines whether a snapshot should be created.
    /// </summary>
    bool ShouldCreateSnapshot(string streamName, string streamId, long streamVersion);
}
