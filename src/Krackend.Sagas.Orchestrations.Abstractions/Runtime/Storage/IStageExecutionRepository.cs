using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists stage execution state.
/// </summary>
public interface IStageExecutionRepository
{
    Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default);

    Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default);

    Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(
        Id instanceId,
        CancellationToken cancellationToken = default);
}
