namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Actions
{
    /// <summary>
    /// Represents a durable runtime action that executes orchestration task work.
    /// </summary>
    public interface ITaskAction
    {
        /// <summary>
        /// Executes the task action.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the action execution.</param>
        /// <returns>A task that completes when the action has finished.</returns>
        Task ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
