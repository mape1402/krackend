namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>
/// Reconciles persisted compensation delivery intents that were interrupted before completion.
/// </summary>
internal interface IOrchestrationCompensationRecoveryCoordinator
{
    /// <summary>
    /// Attempts to recover a bounded set of pending compensation deliveries.
    /// </summary>
    /// <param name="take">Maximum number of pending compensations to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of compensation delivery intents handled.</returns>
    Task<int> RecoverPendingCompensationsAsync(int take, CancellationToken cancellationToken = default);
}
