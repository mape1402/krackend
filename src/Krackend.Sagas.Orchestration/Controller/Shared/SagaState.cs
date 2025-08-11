namespace Krackend.Sagas.Orchestration.Controller.Shared
{
    /// <summary>
    /// Represents the direction of a saga's execution state.
    /// </summary>
    public enum SagaState
    {
        /// <summary>
        /// Indicates the saga is moving forward.
        /// </summary>
        Forward,

        /// <summary>
        /// Indicates the saga is moving backward (compensation).
        /// </summary>
        Backward
    }
}
