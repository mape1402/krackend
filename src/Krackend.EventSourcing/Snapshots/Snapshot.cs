namespace Krackend.EventSourcing.Snapshots;

using Krackend.EventSourcing.Contracts;

/// <summary>
/// Represents an aggregate snapshot.
/// </summary>
public sealed record Snapshot(
    string StreamName,
    string StreamId,
    long StreamVersion,
    string StateType,
    SemanticVersion StateSchemaVersion,
    string Payload,
    DateTimeOffset CreatedAt);
