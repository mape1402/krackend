namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Resolves runtime adapters by task kind.
/// </summary>
public interface ITaskRuntimeAdapterRegistry
{
    /// <summary>
    /// Tries to get the adapter for a task kind.
    /// </summary>
    bool TryGet(TaskKind taskKind, out ITaskRuntimeAdapter adapter);
}
