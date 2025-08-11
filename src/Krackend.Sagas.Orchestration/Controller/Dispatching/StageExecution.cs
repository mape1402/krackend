namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    /// <summary>
    /// Represents the execution state of a stage in saga orchestration.
    /// </summary>
    internal class StageExecution : IStageExecution
    {
        /// <summary>
        /// Gets or sets the identifier of the stage execution.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// Gets or sets the status of the stage execution.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the result of the stage execution.
        /// </summary>
        public string Result { get; set; }

        public StepInformation StepForward { get; init; }

        public StepInformation Compensation {  get; init; }
    }
}
