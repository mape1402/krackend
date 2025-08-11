namespace Krackend.Sagas.Orchestration.Controller.Tracking.Models
{
    /// <summary>
    /// Represents the possible statuses of a saga instance.
    /// </summary>
    public enum SagaStatus
    {
        /// <summary>
        /// The saga is in progress.
        /// </summary>
        InProgress,

        /// <summary>
        /// The saga has completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// The saga has failed.
        /// </summary>
        Failed
    }
}
