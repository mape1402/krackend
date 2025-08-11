namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    /// <summary>
    /// Defines methods for accessing stage execution information in saga orchestration.
    /// </summary>
    public interface IStageExecution
    {
        /// <summary>
        /// Gets the stage identifier.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the forward step information for the stage.
        /// </summary>
        StepInformation StepForward { get; }

        /// <summary>
        /// Gets the compensation step information for the stage.
        /// </summary>
        StepInformation Compensation { get; }
    }
}
