namespace Krackend.EventSourcing.Stores;

/// <summary>
/// Represents an optimistic concurrency conflict while appending events.
/// </summary>
public sealed class EventStoreConcurrencyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreConcurrencyException"/> class.
    /// </summary>
    public EventStoreConcurrencyException(string streamName, string streamId, long expectedVersion, long actualVersion)
        : base($"Stream '{streamName}/{streamId}' expected version '{expectedVersion}' but actual version is '{actualVersion}'.")
    {
        StreamName = streamName;
        StreamId = streamId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>
    /// Gets the logical stream name.
    /// </summary>
    public string StreamName { get; }

    /// <summary>
    /// Gets the stream identifier.
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    /// Gets the expected stream version.
    /// </summary>
    public long ExpectedVersion { get; }

    /// <summary>
    /// Gets the actual stream version.
    /// </summary>
    public long ActualVersion { get; }
}
