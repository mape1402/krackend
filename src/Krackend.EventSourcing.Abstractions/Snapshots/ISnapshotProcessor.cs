namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Processes snapshot candidates for one state type.
/// </summary>
public interface ISnapshotProcessor<TState>
{
    /// <summary>
    /// Processes a single candidate.
    /// </summary>
    Task<SnapshotProcessingResult> ProcessAsync(
        SnapshotCandidate candidate,
        TState initialState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes pending candidates.
    /// </summary>
    Task<IReadOnlyCollection<SnapshotProcessingResult>> ProcessPendingAsync(
        TState initialState,
        int maxCount,
        CancellationToken cancellationToken = default);
}
