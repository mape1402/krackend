namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Represents the result of processing a snapshot candidate.
/// </summary>
public sealed record SnapshotProcessingResult(
    string StreamName,
    string StreamId,
    long SnapshotVersion,
    bool SnapshotSaved);
