namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// In-memory snapshot store for tests and local development.
/// </summary>
public sealed class InMemorySnapshotStore : ISnapshotStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<SnapshotKey, List<Snapshot>> _snapshots = [];

    /// <inheritdoc />
    public Task<Snapshot?> LoadLatestAsync(string streamName, string streamId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_snapshots.TryGetValue(new SnapshotKey(streamName, streamId), out var snapshots))
                return Task.FromResult<Snapshot?>(null);

            return Task.FromResult(snapshots.OrderByDescending(snapshot => snapshot.StreamVersion).FirstOrDefault());
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var key = new SnapshotKey(snapshot.StreamName, snapshot.StreamId);

            if (!_snapshots.TryGetValue(key, out var snapshots))
            {
                snapshots = [];
                _snapshots[key] = snapshots;
            }

            snapshots.Add(snapshot);
            return Task.CompletedTask;
        }
    }

    private readonly record struct SnapshotKey(string StreamName, string StreamId);
}
