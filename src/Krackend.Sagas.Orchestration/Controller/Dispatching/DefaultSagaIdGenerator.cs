namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    /// <summary>
    /// Provides a default implementation for generating unique saga identifiers using ULID.
    /// </summary>
    internal class DefaultSagaIdGenerator : ISagaIdGenerator
    {
        /// <summary>
        /// Creates a new unique saga identifier asynchronously using ULID.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The generated saga identifier as a string.</returns>
        public ValueTask<string> CreateNew(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Ulid.NewUlid().ToString());
    }
}
