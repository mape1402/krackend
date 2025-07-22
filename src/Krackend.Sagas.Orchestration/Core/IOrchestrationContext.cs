namespace Krackend.Sagas.Orchestration.Core
{
    /// <summary>
    /// Provides access to orchestration context information, including metadata, error details, and status for saga orchestration messages.
    /// </summary>
    public interface IOrchestrationContext
    {
        /// <summary>
        /// Determines whether the context contains orchestration metadata.
        /// </summary>
        /// <returns><c>true</c> if metadata is present; otherwise, <c>false</c>.</returns>
        bool HasMetadata();

        /// <summary>
        /// Gets the <see cref="Metadata"/> associated with the orchestration context.
        /// </summary>
        /// <returns>The <see cref="Metadata"/> instance.</returns>
        Metadata GetMetadata();

        /// <summary>
        /// Determines whether the context contains error metadata.
        /// </summary>
        /// <returns><c>true</c> if error metadata is present; otherwise, <c>false</c>.</returns>
        bool HasError();

        /// <summary>
        /// Gets the <see cref="ErrorMetadata"/> associated with the orchestration context, if any error has occurred.
        /// </summary>
        /// <returns>The <see cref="ErrorMetadata"/> instance, or <c>null</c> if no error is present.</returns>
        ErrorMetadata GetErrorMetadata();
    }
}
