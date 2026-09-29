namespace Krackend.EventSourcing.Envelopes;

using Krackend.EventSourcing.Contracts;

/// <summary>
/// Provides the event context used to create a persisted event identifier.
/// </summary>
public sealed record EventIdFactoryContext(
    string StreamName,
    string StreamId,
    string? StreamType,
    long StreamVersion,
    string EventType,
    SemanticVersion EventSchemaVersion,
    object SourceEvent);
