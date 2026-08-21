namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching
{
    /// <summary>
    /// Schedules or executes remote commands requested by the orchestration engine.
    /// </summary>
    public interface IRemoteCommandDispatcher
    {
        /// <summary>
        /// Dispatches a remote command through the configured mechanism.
        /// </summary>
        /// <param name="command">Remote command to dispatch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous dispatch operation.</returns>
        Task DispatchAsync(RemoteCommand command, CancellationToken cancellationToken = default);
    }
}
