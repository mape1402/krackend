namespace Krackend.EventSourcing.Projections;

/// <summary>
/// In-memory checkpoint store for tests and local development.
/// </summary>
public sealed class InMemoryCheckpointStore : ICheckpointStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<CheckpointKey, long> _positions = [];

    /// <inheritdoc />
    public Task<long> GetLastPositionAsync(
        string projectionName,
        string streamName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _positions.TryGetValue(new CheckpointKey(projectionName, streamName), out var position);
            return Task.FromResult(position);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(
        string projectionName,
        string streamName,
        long globalPosition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        if (globalPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(globalPosition), "Global position cannot be negative.");

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _positions[new CheckpointKey(projectionName, streamName)] = globalPosition;
            return Task.CompletedTask;
        }
    }

    private readonly record struct CheckpointKey(string ProjectionName, string StreamName);
}
