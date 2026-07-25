namespace Krackend.EventSourcing.Streams;

/// <summary>
/// Resolves the logical stream name and stream type for events and aggregates.
/// </summary>
public interface IEventStreamResolver
{
    /// <summary>
    /// Resolves the logical stream name for an event CLR type.
    /// </summary>
    string ResolveStreamName(Type eventType);

    /// <summary>
    /// Resolves a stream type from an aggregate CLR type.
    /// </summary>
    string ResolveStreamType(Type aggregateType);
}
