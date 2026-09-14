namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching
{
    /// <summary>
    /// Executes remote orchestration commands through a configured transport adapter.
    /// </summary>
    public interface IRemoteCommandExecutor 
    {
        /// <summary>
        /// Executes the supplied remote command.
        /// </summary>
        /// <param name="command">Remote command to execute.</param>
        /// <param name="cancellationToken">Token used to cancel command execution.</param>
        /// <returns>A task that completes when the command has been dispatched.</returns>
        Task ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken = default);
    }
}
