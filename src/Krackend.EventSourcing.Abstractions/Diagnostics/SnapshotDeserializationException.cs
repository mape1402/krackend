namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when a snapshot payload cannot be deserialized into the requested state type.
/// </summary>
public sealed class SnapshotDeserializationException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotDeserializationException"/> class.
    /// </summary>
    public SnapshotDeserializationException(string streamName, string streamId, Type stateType)
        : base($"Snapshot for stream '{streamName}/{streamId}' could not be deserialized as '{stateType.FullName}'.")
    {
        StreamName = streamName;
        StreamId = streamId;
        StateType = stateType;
    }

    /// <summary>
    /// Gets the stream name.
    /// </summary>
    public string StreamName { get; }

    /// <summary>
    /// Gets the stream identifier.
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    /// Gets the requested state CLR type.
    /// </summary>
    public Type StateType { get; }
}
