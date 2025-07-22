namespace Krackend.Sagas.Orchestration.Core
{
    /// <summary>
    /// Represents metadata information about an error that occurred during orchestration processing.
    /// </summary>
    public sealed class ErrorMetadata
    {
        /// <summary>
        /// Gets or sets the error message describing the failure.
        /// </summary>
        public string Message { get; internal set; }

        /// <summary>
        /// Gets or sets the stack trace associated with the error.
        /// </summary>
        public string StackTrace { get; internal set; }

        /// <summary>
        /// Gets or sets the error code representing the type or category of the error.
        /// </summary>
        public int Code { get; internal set; }
    }
}
