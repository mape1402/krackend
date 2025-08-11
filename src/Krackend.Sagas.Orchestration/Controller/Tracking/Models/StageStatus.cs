namespace Krackend.Sagas.Orchestration.Controller.Tracking.Models
{
    /// <summary>
    /// Represents the possible statuses of a stage in a saga.
    /// </summary>
    public enum StageStatus
    {
        /// <summary>
        /// The stage is pending execution.
        /// </summary>
        Pending,

        /// <summary>
        /// The stage is currently in progress.
        /// </summary>
        InProgress,

        /// <summary>
        /// The stage has completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// The stage has failed.
        /// </summary>
        Failed
    }
}
