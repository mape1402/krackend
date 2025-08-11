namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    /// <summary>
    /// Defines methods for executing commands in saga orchestration.
    /// </summary>
    internal interface ICommander
    {
        /// <summary>
        /// Executes a command asynchronously.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CommandAsync(ICommand command, CancellationToken cancellationToken = default);
    }
}
