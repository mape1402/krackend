using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists task execution state.
/// </summary>
public interface ITaskExecutionRepository
{
    Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default);

    Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default);

    Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default);

    Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(
        Id instanceId,
        CancellationToken cancellationToken = default);
}
