namespace Krackend.Sagas.Orchestration.Controller.Configuration.Metadata
{
    /// <summary>
    /// Represents the metadata for a stage in saga orchestration.
    /// </summary>
    public class StageMetadata
    {
        /// <summary>
        /// Gets or sets the identifier of the stage.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the stage.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the stage.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the metadata for the forward step of the stage.
        /// </summary>
        public StepMetadata StepForward { get; set; }

        /// <summary>
        /// Gets or sets the metadata for the compensation step of the stage.
        /// </summary>
        public StepMetadata Compensation { get; set; }
    }
}
