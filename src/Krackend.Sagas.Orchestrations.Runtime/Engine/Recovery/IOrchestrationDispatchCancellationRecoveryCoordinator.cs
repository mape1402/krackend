using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>
/// Reconciles durable physical-cancellation requests after provider interruptions or process restarts.
/// </summary>
public interface IOrchestrationDispatchCancellationRecoveryCoordinator
{
    /// <summary>
    /// Reconciles one dispatch immediately after its delivery-provider reference becomes available.
    /// </summary>
    /// <param name="dispatchId">Dispatch identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A value indicating whether a pending cancellation was reconciled.</returns>
    Task<bool> ReconcileAsync(Id dispatchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles a bounded set of durable physical-cancellation requests.
    /// </summary>
    /// <param name="take">Maximum number of dispatches to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of cancellation requests attempted.</returns>
    Task<int> ReconcilePendingAsync(int take, CancellationToken cancellationToken = default);
}
