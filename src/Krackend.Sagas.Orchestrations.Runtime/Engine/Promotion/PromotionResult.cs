namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion
{
    /// <summary>
    /// Represents the result of promoting an ingress trigger into an orchestration instance.
    /// </summary>
    public class PromotionResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether promotion succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the promotion error message when promotion failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the saga id created for the promoted orchestration.
        /// </summary>
        public string SagaId { get; set; }

        /// <summary>
        /// Gets or sets the runtime orchestration instance id.
        /// </summary>
        public string InstanceId { get; set; }
    }
}
