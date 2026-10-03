namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

internal sealed class TaskRuntimeAdapterRegistry : ITaskRuntimeAdapterRegistry
{
    private readonly IReadOnlyDictionary<TaskKind, ITaskRuntimeAdapter> _adapters;

    public TaskRuntimeAdapterRegistry(IEnumerable<ITaskRuntimeAdapter> adapters)
    {
        _adapters = (adapters ?? Enumerable.Empty<ITaskRuntimeAdapter>())
            .GroupBy(adapter => adapter.TaskKind)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    public bool TryGet(TaskKind taskKind, out ITaskRuntimeAdapter adapter)
        => _adapters.TryGetValue(taskKind, out adapter);
}
