namespace Krackend.EventSourcing.Streams;

/// <summary>
/// Resolves the event stream for a command.
/// </summary>
public interface ICommandStreamResolver<in TCommand>
{
    /// <summary>
    /// Resolves the event stream used by the command.
    /// </summary>
    EventStreamReference Resolve(TCommand command);
}
