using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

/// <summary>
/// Coordinates exclusive mutation access to one orchestration instance across runtime nodes.
/// </summary>
public interface IOrchestrationInstanceCoordinator
{
    /// <summary>
    /// Executes an operation only when the current runtime can acquire the instance lease.
    /// </summary>
    /// <param name="instanceId">Orchestration instance id.</param>
    /// <param name="operation">Operation to execute while holding the lease.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the operation ran; otherwise <c>false</c>.</returns>
    Task<bool> TryExecuteAsync(
        Id instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
