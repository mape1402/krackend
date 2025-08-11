namespace Krackend.Sagas.Orchestration.Controller.Tracking.Models
{
    /// <summary>
    /// Represents the execution details of a stage in a saga, including attempts and timing.
    /// </summary>
    public class StageExecution
    {
        /// <summary>
        /// Gets or sets the destination to resolve to for this stage execution.
        /// </summary>
        public string ResolveTo { get; init; }

        /// <summary>
        /// Gets or sets the start time of the stage execution.
        /// </summary>
        public DateTimeOffset StartedOn { get; internal set; }

        /// <summary>
        /// Gets or sets the completion time of the stage execution.
        /// </summary>
        public DateTimeOffset? CompletedOn { get; internal set; }

        /// <summary>
        /// Gets or sets the collection of attempts for this stage execution.
        /// </summary>
        public IEnumerable<Attempt> Attempts { get; internal set; }
    }
}
