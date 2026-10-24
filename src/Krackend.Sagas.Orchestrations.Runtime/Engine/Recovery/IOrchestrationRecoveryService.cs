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
    /// Replays one failed or timed out task by creating a new execution attempt.
    /// </summary>
    /// <param name="instanceId">Orchestration instance id.</param>
    /// <param name="taskExecutionId">Task execution id.</param>
    /// <param name="payload">Optional replay payload override.</param>
    /// <param name="reason">Optional operator reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recovery operation result.</returns>
    Task<OrchestrationRecoveryResult> ReplayTaskAsync(
        string instanceId,
        string taskExecutionId,
        string payload = null,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays a failed durable delivery without creating a new task attempt.
    /// </summary>
    /// <param name="instanceId">Orchestration instance id.</param>
    /// <param name="taskExecutionId">Task execution id.</param>
    /// <param name="dispatchId">Task dispatch id.</param>
    /// <param name="reason">Optional operator reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recovery operation result.</returns>
    Task<OrchestrationRecoveryResult> ReplayDispatchAsync(
        string instanceId,
        string taskExecutionId,
        string dispatchId,
        string reason = null,
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
