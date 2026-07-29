namespace Krackend.EventSourcing.Registry;

using Krackend.EventSourcing.Contracts;
using Krackend.EventSourcing.Diagnostics;
using System.Reflection;

/// <summary>
/// Default in-memory state schema registry.
/// </summary>
public sealed class StateSchemaRegistry : IStateSchemaRegistry
{
    private readonly Dictionary<Type, StateSchemaRegistration> _byClrType = [];
    private readonly Dictionary<StateSchemaKey, Type> _byStoredType = [];

    /// <summary>
    /// Registers a state type using its <see cref="StateSchemaAttribute"/> or explicit schema values.
    /// </summary>
    public StateSchemaRegistry Register<TState>(string? stateType = null, SemanticVersion stateSchemaVersion = default)
        => Register(typeof(TState), stateType, stateSchemaVersion);

    /// <summary>
    /// Registers a state type using its <see cref="StateSchemaAttribute"/> or explicit schema values.
    /// </summary>
    public StateSchemaRegistry Register(Type clrType, string? stateType = null, SemanticVersion stateSchemaVersion = default)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        var schema = clrType.GetCustomAttribute<StateSchemaAttribute>();
        var hasExplicitStateType = !string.IsNullOrWhiteSpace(stateType);

        if (schema is null && !hasExplicitStateType)
            throw new StateSchemaMissingException(clrType);

        if (stateSchemaVersion == default)
            stateSchemaVersion = schema?.Version ?? SemanticVersion.Default;

        var resolvedStateType = hasExplicitStateType ? stateType! : schema!.Name;
        var registration = new StateSchemaRegistration(clrType, resolvedStateType, stateSchemaVersion);
        var key = new StateSchemaKey(resolvedStateType, stateSchemaVersion);

        if (_byStoredType.TryGetValue(key, out var registeredType) && registeredType != clrType)
            throw new DuplicateStateSchemaException(resolvedStateType, stateSchemaVersion, registeredType, clrType);

        _byClrType[clrType] = registration;
        _byStoredType[key] = clrType;

        return this;
    }

    /// <inheritdoc />
    public StateSchemaRegistration GetRegistration(Type stateType)
    {
        ArgumentNullException.ThrowIfNull(stateType);

        if (!_byClrType.TryGetValue(stateType, out var registration))
            throw new StateTypeNotRegisteredException(stateType);

        return registration;
    }

    /// <inheritdoc />
    public Type Resolve(string stateType, SemanticVersion stateSchemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateType);

        if (stateSchemaVersion == default)
            stateSchemaVersion = SemanticVersion.Default;

        if (_byStoredType.TryGetValue(new StateSchemaKey(stateType, stateSchemaVersion), out var clrType))
            return clrType;

        throw new StateTypeNotRegisteredException(stateType, stateSchemaVersion);
    }

    private readonly record struct StateSchemaKey(string StateType, SemanticVersion StateSchemaVersion);
}
