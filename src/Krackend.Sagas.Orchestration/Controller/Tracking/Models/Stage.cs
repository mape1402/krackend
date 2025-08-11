namespace Krackend.Sagas.Orchestration.Controller.Tracking.Models
{
    /// <summary>
    /// Represents a stage in a saga, including its status and execution steps.
    /// </summary>
    public class Stage
    {
        /// <summary>
        /// Gets or sets the stage identifier.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// Gets or sets the stage name.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets or sets the status of the stage.
        /// </summary>
        public StageStatus Status { get; internal set; }

        /// <summary>
        /// Gets or sets the execution details for the forward step of the stage.
        /// </summary>
        public StageExecution StepForward { get; internal set; }

        /// <summary>
        /// Gets or sets the execution details for the compensation step of the stage.
        /// </summary>
        public StageExecution Compensation { get; internal set; }
    }
}
