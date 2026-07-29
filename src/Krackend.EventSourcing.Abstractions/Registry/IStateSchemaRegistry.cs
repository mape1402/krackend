namespace Krackend.EventSourcing.Registry;

using Krackend.EventSourcing.Contracts;

/// <summary>
/// Resolves CLR state types to persisted state schema names and versions.
/// </summary>
public interface IStateSchemaRegistry
{
    /// <summary>
    /// Gets the schema registration for a previously registered CLR state type.
    /// </summary>
    StateSchemaRegistration GetRegistration(Type stateType);

    /// <summary>
    /// Resolves a persisted state schema name and version to the exact CLR type registered for that schema.
    /// </summary>
    Type Resolve(string stateType, SemanticVersion stateSchemaVersion);
}
