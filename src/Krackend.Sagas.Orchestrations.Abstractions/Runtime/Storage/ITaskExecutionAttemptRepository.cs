using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists task execution attempts.
/// </summary>
public interface ITaskExecutionAttemptRepository
{
    Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default);

    Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default);

    Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default);

    Task<TaskExecutionAttempt> GetByDispatchId(Id dispatchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(
        Id taskExecutionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets attempts still waiting for response whose waiting timestamp is older than or equal to the provided cutoff.
    /// </summary>
    /// <param name="dueBeforeUtc">Maximum waiting timestamp to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Waiting response task attempts.</returns>
    Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(
        DateTime dueBeforeUtc,
        CancellationToken cancellationToken = default);
}
