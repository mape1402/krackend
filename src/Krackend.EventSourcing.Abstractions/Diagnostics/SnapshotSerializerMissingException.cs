namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when a snapshot must be loaded but no snapshot serializer is available.
/// </summary>
public sealed class SnapshotSerializerMissingException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotSerializerMissingException"/> class.
    /// </summary>
    public SnapshotSerializerMissingException()
        : base("A snapshot serializer is required to load snapshots.")
    {
    }
}
