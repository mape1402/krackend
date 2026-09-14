namespace Krackend.Sagas.Orchestrations.Runtime.Engine
{
    /// <summary>
    /// Coordinates orchestration instance startup and continuation.
    /// </summary>
    public interface ISagaEngine
    {
        /// <summary>
        /// Starts an orchestration instance from an ingress trigger payload.
        /// </summary>
        /// <param name="intent">Start intent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task StartOrchestrationAsync(StartIntent intent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Continues an orchestration instance from a backchannel callback.
        /// </summary>
        /// <param name="intent">Forward intent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OrchestrateAsync(ForwardIntent intent, CancellationToken cancellationToken = default);
    }
}
