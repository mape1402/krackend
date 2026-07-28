namespace Krackend.EventSourcing.Registry;

using Krackend.EventSourcing.Contracts;

/// <summary>
/// Resolves CLR event types to persisted event names and versions.
/// </summary>
public interface IEventTypeRegistry
{
    /// <summary>
    /// Gets the registration for a CLR event type.
    /// </summary>
    EventTypeRegistration GetRegistration(Type eventType);

    /// <summary>
    /// Resolves a persisted event name and version to a CLR type.
    /// </summary>
    Type Resolve(string eventType, SemanticVersion eventSchemaVersion);

    /// <summary>
    /// Gets the latest registered schema for a persisted event name.
    /// </summary>
    EventTypeRegistration GetLatestRegistration(string eventType);
}
