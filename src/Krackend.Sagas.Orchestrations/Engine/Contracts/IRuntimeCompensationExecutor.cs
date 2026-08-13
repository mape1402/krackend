using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Executes pending runtime compensations.
/// </summary>
public interface IRuntimeCompensationExecutor
{
    /// <summary>
    /// Executes a pending compensation.
    /// </summary>
    /// <param name="compensation">Compensation execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compensation execution result.</returns>
    Task<RuntimeCompensationExecutionResult> Execute(CompensationExecution compensation, CancellationToken cancellationToken = default);
}
