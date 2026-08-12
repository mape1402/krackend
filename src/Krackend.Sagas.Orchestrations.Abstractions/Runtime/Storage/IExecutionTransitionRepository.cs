using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists append-only execution timeline transitions.
/// </summary>
public interface IExecutionTransitionRepository
{
    Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(
        Id instanceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(
        string environmentKey,
        int take = 250,
        CancellationToken cancellationToken = default);
}
