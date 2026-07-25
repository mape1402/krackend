namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Creates snapshots every configured number of events.
/// </summary>
public sealed class IntervalSnapshotStrategy : ISnapshotStrategy
{
    private readonly long _interval;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalSnapshotStrategy"/> class.
    /// </summary>
    public IntervalSnapshotStrategy(long interval)
    {
        if (interval <= 0)
            throw new ArgumentOutOfRangeException(nameof(interval), "Snapshot interval must be greater than zero.");

        _interval = interval;
    }

    /// <inheritdoc />
    public bool ShouldCreateSnapshot(string streamName, string streamId, long streamVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        return streamVersion > 0 && streamVersion % _interval == 0;
    }
}
