namespace Krackend.Sagas.Orchestration.Controller.Shared
{
    using Krackend.Sagas.Orchestration.Controller.Dispatching;

    /// <summary>
    /// Defines a step in saga orchestration.
    /// </summary>
    public interface IStep
    {
        /// <summary>
        /// Processes the saga execution state asynchronously.
        /// </summary>
        /// <param name="state">The execution state of the saga.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ProcessAsync(SagaExecutionState state, CancellationToken cancellationToken = default);
    }
}
