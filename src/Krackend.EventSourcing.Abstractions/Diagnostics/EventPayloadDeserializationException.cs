using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when an event payload cannot be deserialized into its registered CLR type.
/// </summary>
public sealed class EventPayloadDeserializationException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventPayloadDeserializationException"/> class.
    /// </summary>
    public EventPayloadDeserializationException(string eventType, SemanticVersion eventSchemaVersion, Type eventClrType)
        : base($"Event '{eventType}' schema version '{eventSchemaVersion}' could not be deserialized as '{eventClrType.FullName}'.")
    {
        EventType = eventType;
        EventSchemaVersion = eventSchemaVersion;
        EventClrType = eventClrType;
    }

    /// <summary>
    /// Gets the event schema name.
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// Gets the event schema version.
    /// </summary>
    public SemanticVersion EventSchemaVersion { get; }

    /// <summary>
    /// Gets the CLR type selected for deserialization.
    /// </summary>
    public Type EventClrType { get; }
}
