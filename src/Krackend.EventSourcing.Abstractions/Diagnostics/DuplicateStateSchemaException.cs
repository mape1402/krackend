using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when two CLR types are registered for the same state schema name and version.
/// </summary>
public sealed class DuplicateStateSchemaException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateStateSchemaException"/> class.
    /// </summary>
    public DuplicateStateSchemaException(
        string stateType,
        SemanticVersion stateSchemaVersion,
        Type registeredClrType,
        Type duplicateClrType)
        : base($"State schema '{stateType}' version '{stateSchemaVersion}' is already registered for '{registeredClrType.FullName}' and cannot be reused by '{duplicateClrType.FullName}'.")
    {
        StateType = stateType;
        StateSchemaVersion = stateSchemaVersion;
        RegisteredClrType = registeredClrType;
        DuplicateClrType = duplicateClrType;
    }

    /// <summary>
    /// Gets the duplicated state schema name.
    /// </summary>
    public string StateType { get; }

    /// <summary>
    /// Gets the duplicated state schema version.
    /// </summary>
    public SemanticVersion StateSchemaVersion { get; }

    /// <summary>
    /// Gets the CLR type that was already registered.
    /// </summary>
    public Type RegisteredClrType { get; }

    /// <summary>
    /// Gets the CLR type that attempted to reuse the schema.
    /// </summary>
    public Type DuplicateClrType { get; }
}
