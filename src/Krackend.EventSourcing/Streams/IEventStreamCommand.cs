namespace Krackend.EventSourcing.Streams;

/// <summary>
/// Marks a command that can identify its event stream without handler literals.
/// </summary>
public interface IEventStreamCommand
{
    /// <summary>
    /// Gets the stream id for the command.
    /// </summary>
    string StreamId { get; }
}
