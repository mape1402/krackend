namespace Krackend.Sagas.Orchestration.Working
{
    using Krackend.Sagas.Orchestration.Core;

    /// <summary>
    /// Defines a contract for extracting error metadata from exceptions during orchestration processing.
    /// </summary>
    public interface IExceptionEvaluator
    {
        /// <summary>
        /// Extracts <see cref="ErrorMetadata"/> from the provided exception.
        /// </summary>
        /// <param name="exception">The exception from which to extract error metadata.</param>
        /// <returns>An <see cref="ErrorMetadata"/> instance containing details about the exception.</returns>
        ErrorMetadata ExtractMetadata(Exception exception);
    }
}
