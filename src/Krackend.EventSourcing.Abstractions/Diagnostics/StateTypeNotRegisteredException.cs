using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Thrown when a state type cannot be resolved from the registry.
/// </summary>
public sealed class StateTypeNotRegisteredException : EventSourcingException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StateTypeNotRegisteredException"/> class for a CLR lookup.
    /// </summary>
    public StateTypeNotRegisteredException(Type stateClrType)
        : base($"State type '{stateClrType.FullName}' is not registered.")
    {
        StateClrType = stateClrType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateTypeNotRegisteredException"/> class for a stored schema lookup.
    /// </summary>
    public StateTypeNotRegisteredException(string stateType, SemanticVersion stateSchemaVersion)
        : base($"State type '{stateType}' schema version '{stateSchemaVersion}' is not registered.")
    {
        StateType = stateType;
        StateSchemaVersion = stateSchemaVersion;
    }

    /// <summary>
    /// Gets the unresolved CLR type, when the lookup was made by CLR type.
    /// </summary>
    public Type? StateClrType { get; }

    /// <summary>
    /// Gets the unresolved state schema name, when the lookup was made by stored schema.
    /// </summary>
    public string? StateType { get; }

    /// <summary>
    /// Gets the unresolved state schema version, when the lookup was made by stored schema.
    /// </summary>
    public SemanticVersion StateSchemaVersion { get; }
}
