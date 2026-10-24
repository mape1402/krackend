using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>
/// Applies a logical cancellation fence to pending durable task dispatches for an orchestration instance.
/// </summary>
public interface IOrchestrationDispatchCancellationCoordinator
{
    /// <summary>
    /// Cancels dispatches that have not yet been published to the remote transport.
    /// </summary>
    /// <param name="instance">Terminal orchestration instance.</param>
    /// <param name="reason">Cancellation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of dispatches fenced by the runtime.</returns>
    Task<int> CancelPendingAsync(
        OrchestrationInstance instance,
        string reason,
        CancellationToken cancellationToken = default);
}
