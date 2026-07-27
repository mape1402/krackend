namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Stores streams that should be processed by a snapshot worker.
/// </summary>
public interface ISnapshotCandidateStore
{
    /// <summary>
    /// Marks a stream as needing snapshot processing.
    /// </summary>
    Task MarkAsync(
        string streamName,
        string streamId,
        long streamVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets candidates ready for processing.
    /// </summary>
    Task<IReadOnlyCollection<SnapshotCandidate>> GetPendingAsync(
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a candidate as processed.
    /// </summary>
    Task CompleteAsync(
        SnapshotCandidate candidate,
        CancellationToken cancellationToken = default);
}
