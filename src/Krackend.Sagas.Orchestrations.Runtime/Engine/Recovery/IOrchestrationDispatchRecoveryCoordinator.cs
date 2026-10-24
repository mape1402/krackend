namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>
/// Reconciles persisted task delivery intents that were not acknowledged by their execution provider.
/// </summary>
public interface IOrchestrationDispatchRecoveryCoordinator
{
    /// <summary>
    /// Attempts to recover a bounded set of pending durable task deliveries.
    /// </summary>
    /// <param name="take">Maximum number of delivery intents to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of delivery intents recovered or transitioned to their durable failure path.</returns>
    Task<int> RecoverPendingDispatchesAsync(int take, CancellationToken cancellationToken = default);
}
