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

    Task<TaskExecution> GetByStageAndKey(
        Id stageExecutionId,
        string taskKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(
        Id instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks still waiting for response whose waiting timestamp is older than or equal to the provided cutoff.
    /// </summary>
    /// <param name="dueBeforeUtc">Maximum waiting timestamp to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Waiting response task executions.</returns>
    Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(
        DateTime dueBeforeUtc,
        CancellationToken cancellationToken = default);
}
