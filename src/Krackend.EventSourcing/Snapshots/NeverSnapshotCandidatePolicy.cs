namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Disables automatic snapshot candidate marking.
/// </summary>
public sealed class NeverSnapshotCandidatePolicy : ISnapshotCandidatePolicy
{
    /// <inheritdoc />
    public bool ShouldMark(
        string streamName,
        string streamId,
        long previousSnapshotVersion,
        long currentStreamVersion)
        => false;
}
