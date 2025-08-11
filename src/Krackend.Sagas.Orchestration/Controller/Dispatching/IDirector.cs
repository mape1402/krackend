namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    /// <summary>
    /// Defines methods for directing saga orchestration.
    /// </summary>
    public interface IDirector
    {
        /// <summary>
        /// Directs the saga execution asynchronously.
        /// </summary>
        /// <param name="state">The execution state of the saga.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DirectAsync(SagaExecutionState state, CancellationToken cancellationToken = default);
    }
}
