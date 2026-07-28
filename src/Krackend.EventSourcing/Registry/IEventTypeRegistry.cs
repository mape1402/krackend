namespace Krackend.EventSourcing.Registry;

using Krackend.EventSourcing.Contracts;

/// <summary>
/// Resolves CLR event types to persisted event names and versions.
/// </summary>
public interface IEventTypeRegistry
{
    /// <summary>
    /// Gets the schema registration for a previously registered CLR event type.
    /// </summary>
    EventTypeRegistration GetRegistration(Type eventType);

    /// <summary>
    /// Resolves a persisted event name and schema version to the exact CLR type registered for that schema.
    /// </summary>
    Type Resolve(string eventType, SemanticVersion eventSchemaVersion);

}
