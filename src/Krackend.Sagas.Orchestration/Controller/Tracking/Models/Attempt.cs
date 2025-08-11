namespace Krackend.Sagas.Orchestration.Controller.Tracking.Models
{
    /// <summary>
    /// Represents an attempt to execute a step within a saga stage.
    /// </summary>
    public class Attempt
    {
        /// <summary>
        /// Gets or sets the start time of the attempt.
        /// </summary>
        public DateTimeOffset StartedOn { get; init; }

        /// <summary>
        /// Gets or sets the completion time of the attempt.
        /// </summary>
        public DateTimeOffset? CompletedOn { get; init; }

        /// <summary>
        /// Gets or sets the payload sent in the attempt.
        /// </summary>
        public string Payload { get; init; }

        /// <summary>
        /// Gets or sets the response received after the attempt.
        /// </summary>
        public string Response { get; init; }

        /// <summary>
        /// Gets or sets the error produced during the attempt, if any.
        /// </summary>
        public Error Error { get; init; }

        /// <summary>
        /// Gets or sets the final state of the attempt.
        /// </summary>
        public AttemptState State { get; init; }
    }
}
