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

    Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(
        Id taskExecutionId,
        CancellationToken cancellationToken = default);
}
