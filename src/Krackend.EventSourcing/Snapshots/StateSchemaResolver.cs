using Krackend.EventSourcing.Contracts;
using System.Reflection;

namespace Krackend.EventSourcing.Snapshots;

internal static class StateSchemaResolver
{
    public static StateSchemaRegistration Resolve(Type stateType)
    {
        ArgumentNullException.ThrowIfNull(stateType);

        var schema = stateType.GetCustomAttribute<StateSchemaAttribute>();

        if (schema is null)
            throw new InvalidOperationException($"State type '{stateType.FullName}' must be decorated with '{nameof(StateSchemaAttribute)}' before it can be snapshotted.");

        return new StateSchemaRegistration(schema.Name, schema.Version);
    }
}

internal readonly record struct StateSchemaRegistration(string StateType, SemanticVersion StateSchemaVersion);
