namespace Krackend.Sagas.Orchestration.Controller.Tracking.Models
{
    /// <summary>
    /// Represents an error that occurred during a saga attempt.
    /// </summary>
    public class Error
    {
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the stack trace of the error.
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public int Code { get; set; }
    }
}
