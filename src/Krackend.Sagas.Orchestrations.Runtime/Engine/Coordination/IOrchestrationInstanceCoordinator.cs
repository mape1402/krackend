using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

/// <summary>
/// Coordinates exclusive mutation access to one orchestration instance across runtime nodes.
/// </summary>
/// <remarks>
/// A coordinator is scoped to one unit of work. Nested calls in that scope may reuse its lease;
/// independent concurrent work must use a separate dependency-injection scope.
/// </remarks>
public interface IOrchestrationInstanceCoordinator
{
    /// <summary>
    /// Executes an operation only when the current runtime can acquire the instance lease.
    /// </summary>
    /// <param name="instanceId">Orchestration instance id.</param>
    /// <param name="operation">Operation to execute while holding the lease.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="waitForLease">Whether to wait for a busy lease before skipping the operation.</param>
    /// <returns><c>true</c> when the operation ran; otherwise <c>false</c>.</returns>
    Task<bool> TryExecuteAsync(
        Id instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default,
        bool waitForLease = true);
}

/// <summary>
/// Coordinates creation of an instance whose identifier has not been persisted yet.
/// </summary>
/// <remarks>
/// A proposed distributed identifier has no row that can be leased. This capability lets
/// providers preserve the atomic creation transaction without issuing a guaranteed-miss
/// lease update and lookup before every new orchestration.
/// </remarks>
public interface INewOrchestrationInstanceCoordinator
{
    /// <summary>Executes the atomic first mutation for a not-yet-persisted instance.</summary>
    Task<bool> TryExecuteNewAsync(
        Id proposedInstanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default,
        bool waitForLocalGate = true);
}
