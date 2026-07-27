namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Disables automatic snapshot creation.
/// </summary>
public sealed class NeverSnapshotStrategy : ISnapshotStrategy
{
    /// <inheritdoc />
    public bool ShouldCreateSnapshot(string streamName, string streamId, long streamVersion)
        => false;
}
