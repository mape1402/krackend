namespace Krackend.Sagas.Orchestration.Controller.Tracking.Models
{
    /// <summary>
    /// Represents the state of an attempt in a saga stage execution.
    /// </summary>
    public enum AttemptState
    {
        /// <summary>
        /// The attempt succeeded.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The attempt failed.
        /// </summary>
        Failed,
    }
}
