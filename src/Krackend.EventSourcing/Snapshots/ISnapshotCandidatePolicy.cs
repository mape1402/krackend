namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Decides whether a stream should be marked as a snapshot candidate after append.
/// </summary>
public interface ISnapshotCandidatePolicy
{
    /// <summary>
    /// Determines whether a stream should be marked as a snapshot candidate.
    /// </summary>
    bool ShouldMark(
        string streamName,
        string streamId,
        long previousSnapshotVersion,
        long currentStreamVersion);
}
