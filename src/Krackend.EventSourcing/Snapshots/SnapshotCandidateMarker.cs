namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Default snapshot candidate marker.
/// </summary>
public sealed class SnapshotCandidateMarker : ISnapshotCandidateMarker
{
    private readonly ISnapshotStore _snapshotStore;
    private readonly ISnapshotCandidateStore _candidateStore;
    private readonly ISnapshotCandidatePolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotCandidateMarker"/> class.
    /// </summary>
    public SnapshotCandidateMarker(
        ISnapshotStore snapshotStore,
        ISnapshotCandidateStore candidateStore,
        ISnapshotCandidatePolicy policy)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _candidateStore = candidateStore ?? throw new ArgumentNullException(nameof(candidateStore));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <inheritdoc />
    public async Task MarkIfNeededAsync(
        string streamName,
        string streamId,
        long currentStreamVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        if (currentStreamVersion <= 0)
            return;

        var snapshot = await _snapshotStore.LoadLatestAsync(streamName, streamId, cancellationToken);
        var snapshotVersion = snapshot?.StreamVersion ?? 0;

        if (!_policy.ShouldMark(streamName, streamId, snapshotVersion, currentStreamVersion))
            return;

        await _candidateStore.MarkAsync(streamName, streamId, currentStreamVersion, cancellationToken);
    }
}
