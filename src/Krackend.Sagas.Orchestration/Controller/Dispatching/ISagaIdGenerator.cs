namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    /// <summary>
    /// Defines methods for generating unique saga identifiers.
    /// </summary>
    internal interface ISagaIdGenerator
    {
        /// <summary>
        /// Creates a new saga identifier asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The generated identifier.</returns>
        ValueTask<string> CreateNew(CancellationToken cancellationToken = default);
    }
}
