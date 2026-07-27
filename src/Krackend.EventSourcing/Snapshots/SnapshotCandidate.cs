namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Represents a stream that should be considered for snapshot creation.
/// </summary>
public sealed record SnapshotCandidate(
    string StreamName,
    string StreamId,
    long StreamVersion,
    DateTimeOffset MarkedAt);
