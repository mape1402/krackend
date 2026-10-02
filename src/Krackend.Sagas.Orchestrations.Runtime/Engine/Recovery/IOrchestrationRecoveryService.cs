namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>
/// Provides operator-driven recovery operations for orchestration instances.
/// </summary>
public interface IOrchestrationRecoveryService
{
    /// <summary>
    /// Replays a recoverable orchestration from the point where it stopped.
    /// </summary>
    /// <param name="instanceId">Orchestration instance id.</param>
    /// <param name="payload">Optional replay payload override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recovery operation result.</returns>
    Task<OrchestrationRecoveryResult> ReplayAsync(
        string instanceId,
        string payload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts an orchestration instance after operator review.
    /// </summary>
    /// <param name="instanceId">Orchestration instance id.</param>
    /// <param name="reason">Abort reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recovery operation result.</returns>
    Task<OrchestrationRecoveryResult> AbortAsync(
        string instanceId,
        string reason = null,
        CancellationToken cancellationToken = default);
}
