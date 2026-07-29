namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// EF Core record used to persist stream snapshots.
/// </summary>
public sealed class EventSnapshotRecord
{
    /// <summary>
    /// Gets or sets the snapshot id.
    /// </summary>
    public Guid SnapshotId { get; set; }

    /// <summary>
    /// Gets or sets the logical stream name.
    /// </summary>
    public string StreamName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream id.
    /// </summary>
    public string StreamId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream version captured by the snapshot.
    /// </summary>
    public long StreamVersion { get; set; }

    /// <summary>
    /// Gets or sets the persisted state schema name.
    /// </summary>
    public string StateType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the persisted state schema version.
    /// </summary>
    public string StateSchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized snapshot payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the snapshot creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
