namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Marks streams after a configured number of events since the latest snapshot.
/// </summary>
public sealed class IntervalSnapshotCandidatePolicy : ISnapshotCandidatePolicy
{
    private readonly long _interval;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalSnapshotCandidatePolicy"/> class.
    /// </summary>
    public IntervalSnapshotCandidatePolicy(long interval)
    {
        if (interval <= 0)
            throw new ArgumentOutOfRangeException(nameof(interval), "Snapshot candidate interval must be greater than zero.");

        _interval = interval;
    }

    /// <inheritdoc />
    public bool ShouldMark(
        string streamName,
        string streamId,
        long previousSnapshotVersion,
        long currentStreamVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        return currentStreamVersion > previousSnapshotVersion
            && currentStreamVersion - previousSnapshotVersion >= _interval;
    }
}
