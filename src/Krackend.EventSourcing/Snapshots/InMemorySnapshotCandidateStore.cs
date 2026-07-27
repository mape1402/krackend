namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// In-memory snapshot candidate store for tests and local samples.
/// </summary>
public sealed class InMemorySnapshotCandidateStore : ISnapshotCandidateStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<CandidateKey, SnapshotCandidate> _candidates = [];

    /// <inheritdoc />
    public Task MarkAsync(
        string streamName,
        string streamId,
        long streamVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        if (streamVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(streamVersion), "Stream version must be greater than zero.");

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _candidates[new CandidateKey(streamName, streamId)] = new SnapshotCandidate(
                streamName,
                streamId,
                streamVersion,
                DateTimeOffset.UtcNow);

            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<SnapshotCandidate>> GetPendingAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be greater than zero.");

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyCollection<SnapshotCandidate>>(
                _candidates.Values
                    .OrderBy(candidate => candidate.MarkedAt)
                    .Take(maxCount)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task CompleteAsync(
        SnapshotCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _candidates.Remove(new CandidateKey(candidate.StreamName, candidate.StreamId));
            return Task.CompletedTask;
        }
    }

    private readonly record struct CandidateKey(string StreamName, string StreamId);
}
