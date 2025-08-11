namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    /// <summary>
    /// Defines methods for managing the execution state of a saga, including creation and retrieval of the current state.
    /// </summary>
    public interface IStateManager
    {
        /// <summary>
        /// Creates a new saga execution state for the specified topic.
        /// </summary>
        /// <param name="topic">The topic for which to create the state.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation and containing the saga execution state.</returns>
        Task<SagaExecutionState> CreateState(string topic, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current saga execution state.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation and containing the saga execution state.</returns>
        Task<SagaExecutionState> GetCurrentState(CancellationToken cancellationToken = default);
    }
}
