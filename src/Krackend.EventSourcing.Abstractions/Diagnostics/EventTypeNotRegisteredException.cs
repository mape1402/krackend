using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when an event type cannot be resolved from the registry.
/// </summary>
public sealed class EventTypeNotRegisteredException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventTypeNotRegisteredException"/> class for a CLR lookup.
    /// </summary>
    public EventTypeNotRegisteredException(Type eventClrType)
        : base($"Event type '{eventClrType.FullName}' is not registered.")
    {
        EventClrType = eventClrType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventTypeNotRegisteredException"/> class for a stored schema lookup.
    /// </summary>
    public EventTypeNotRegisteredException(string eventType, SemanticVersion eventSchemaVersion)
        : base($"Event type '{eventType}' schema version '{eventSchemaVersion}' is not registered.")
    {
        EventType = eventType;
        EventSchemaVersion = eventSchemaVersion;
    }

    /// <summary>
    /// Gets the unresolved CLR type, when the lookup was made by CLR type.
    /// </summary>
    public Type? EventClrType { get; }

    /// <summary>
    /// Gets the unresolved event schema name, when the lookup was made by stored schema.
    /// </summary>
    public string? EventType { get; }

    /// <summary>
    /// Gets the unresolved event schema version, when the lookup was made by stored schema.
    /// </summary>
    public SemanticVersion EventSchemaVersion { get; }
}
