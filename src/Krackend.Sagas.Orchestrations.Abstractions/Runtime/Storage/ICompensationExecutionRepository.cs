using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Stores compensation execution records.
/// </summary>
public interface ICompensationExecutionRepository
{
    /// <summary>
    /// Creates a compensation execution record.
    /// </summary>
    /// <param name="compensationExecution">Compensation execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Create(CompensationExecution compensationExecution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a compensation execution record.
    /// </summary>
    /// <param name="compensationExecution">Compensation execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Update(CompensationExecution compensationExecution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets compensation executions for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">Orchestration instance id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compensation executions.</returns>
    Task<IReadOnlyCollection<CompensationExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets compensation executions waiting to be processed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending compensation executions.</returns>
    Task<IReadOnlyCollection<CompensationExecution>> GetPending(CancellationToken cancellationToken = default);
}
