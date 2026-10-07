namespace Krackend.Sagas.Orchestrations.Runtime.Recovery;

/// <summary>
/// Reconciles durable orchestration state after startup, failover, or dependency restoration.
/// </summary>
public interface IOrchestrationRuntimeReconciler
{
    /// <summary>
    /// Runs one reconciliation pass.
    /// </summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Reconciliation result.</returns>
    Task<RuntimeReconciliationResult> ReconcileAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
