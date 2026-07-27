namespace Krackend.EventSourcing.Streams;

/// <summary>
/// Identifies the stream used to load and append events for one command.
/// </summary>
public readonly record struct EventStreamReference(string Name, string Id)
{
    /// <summary>
    /// Creates a stream reference.
    /// </summary>
    public static EventStreamReference Create(string name, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return new EventStreamReference(name, id);
    }
}
