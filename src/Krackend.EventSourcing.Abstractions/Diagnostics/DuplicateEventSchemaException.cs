using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when two CLR types are registered for the same event schema name and version.
/// </summary>
public sealed class DuplicateEventSchemaException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateEventSchemaException"/> class.
    /// </summary>
    public DuplicateEventSchemaException(
        string eventType,
        SemanticVersion eventSchemaVersion,
        Type registeredClrType,
        Type duplicateClrType)
        : base($"Event schema '{eventType}' version '{eventSchemaVersion}' is already registered for '{registeredClrType.FullName}' and cannot be reused by '{duplicateClrType.FullName}'.")
    {
        EventType = eventType;
        EventSchemaVersion = eventSchemaVersion;
        RegisteredClrType = registeredClrType;
        DuplicateClrType = duplicateClrType;
    }

    /// <summary>
    /// Gets the duplicated event schema name.
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// Gets the duplicated event schema version.
    /// </summary>
    public SemanticVersion EventSchemaVersion { get; }

    /// <summary>
    /// Gets the CLR type that was already registered.
    /// </summary>
    public Type RegisteredClrType { get; }

    /// <summary>
    /// Gets the CLR type that attempted to reuse the schema.
    /// </summary>
    public Type DuplicateClrType { get; }
}
