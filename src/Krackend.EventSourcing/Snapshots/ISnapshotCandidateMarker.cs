namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Marks streams for asynchronous snapshot processing.
/// </summary>
public interface ISnapshotCandidateMarker
{
    /// <summary>
    /// Marks a stream as a snapshot candidate when policy allows it.
    /// </summary>
    Task MarkIfNeededAsync(
        string streamName,
        string streamId,
        long currentStreamVersion,
        CancellationToken cancellationToken = default);
}
