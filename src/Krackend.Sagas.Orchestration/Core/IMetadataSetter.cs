namespace Krackend.Sagas.Orchestration.Core
{
    /// <summary>
    /// Defines methods for setting orchestration metadata and marking the result as success or failure on an implementing class.
    /// </summary>
    internal interface IMetadataSetter
    {
        /// <summary>
        /// Sets the provided <see cref="OrchestrationMetadata"/> instance on the implementing object.
        /// </summary>
        /// <param name="metadata">The metadata to set.</param>
        void SetMetadata(OrchestrationMetadata metadata);

        /// <summary>
        /// Marks the orchestration result as a failure on the implementing object and sets the associated error metadata.
        /// </summary>
        /// <param name="errorMetadata">The error metadata to associate with the failure.</param>
        void SetAsFailure(ErrorMetadata errorMetadata);

        /// <summary>
        /// Marks the orchestration result as a success on the implementing object.
        /// </summary>
        void SetAsSuccess();
    }
}
