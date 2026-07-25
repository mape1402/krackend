namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Represents an aggregate snapshot.
/// </summary>
public sealed record Snapshot(
    string StreamName,
    string StreamId,
    long StreamVersion,
    string Payload,
    DateTimeOffset CreatedAt);
