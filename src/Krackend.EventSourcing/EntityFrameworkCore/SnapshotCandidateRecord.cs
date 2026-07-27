namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// EF Core record used to persist streams pending snapshot processing.
/// </summary>
public sealed class SnapshotCandidateRecord
{
    /// <summary>
    /// Gets or sets the logical stream name.
    /// </summary>
    public string StreamName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream id.
    /// </summary>
    public string StreamId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream version that should be processed.
    /// </summary>
    public long StreamVersion { get; set; }

    /// <summary>
    /// Gets or sets when the stream was marked.
    /// </summary>
    public DateTimeOffset MarkedAt { get; set; }
}
