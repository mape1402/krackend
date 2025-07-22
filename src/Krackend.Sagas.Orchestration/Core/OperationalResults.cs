namespace Krackend.Sagas.Orchestration.Core
{
    /// <summary>
    /// Represents the results of an operation within the orchestration, including error and tracing information.
    /// </summary>
    public sealed class OperationalResults
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation resulted in an error.
        /// </summary>
        public bool HasError { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether execution tracing information is available for the operation.
        /// </summary>
        public bool HasExecutionTracing { get; internal set; }
    }
}
