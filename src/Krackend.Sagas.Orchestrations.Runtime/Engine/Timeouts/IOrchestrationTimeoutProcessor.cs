using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

/// <summary>
/// Processes orchestration task executions whose configured response timeout has elapsed.
/// </summary>
public interface IOrchestrationTimeoutProcessor
{
    /// <summary>
    /// Applies timeout policies for waiting task executions due at the supplied UTC instant.
    /// </summary>
    /// <param name="utcNow">Current UTC instant used to evaluate due timeouts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of task executions whose timeout policy was applied.</returns>
    Task<int> ProcessDueTimeoutsAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
