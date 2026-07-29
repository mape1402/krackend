using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when a stored snapshot does not match the requested state schema.
/// </summary>
public sealed class SnapshotStateSchemaMismatchException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotStateSchemaMismatchException"/> class.
    /// </summary>
    public SnapshotStateSchemaMismatchException(
        string snapshotStateType,
        SemanticVersion snapshotStateSchemaVersion,
        string requestedStateType,
        SemanticVersion requestedStateSchemaVersion)
        : base($"Snapshot state schema '{snapshotStateType}' version '{snapshotStateSchemaVersion}' does not match requested state '{requestedStateType}' version '{requestedStateSchemaVersion}'.")
    {
        SnapshotStateType = snapshotStateType;
        SnapshotStateSchemaVersion = snapshotStateSchemaVersion;
        RequestedStateType = requestedStateType;
        RequestedStateSchemaVersion = requestedStateSchemaVersion;
    }

    /// <summary>
    /// Gets the state schema name stored in the snapshot.
    /// </summary>
    public string SnapshotStateType { get; }

    /// <summary>
    /// Gets the state schema version stored in the snapshot.
    /// </summary>
    public SemanticVersion SnapshotStateSchemaVersion { get; }

    /// <summary>
    /// Gets the requested state schema name.
    /// </summary>
    public string RequestedStateType { get; }

    /// <summary>
    /// Gets the requested state schema version.
    /// </summary>
    public SemanticVersion RequestedStateSchemaVersion { get; }
}
