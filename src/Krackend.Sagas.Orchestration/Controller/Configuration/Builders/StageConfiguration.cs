namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    /// <summary>
    /// Represents the configuration data for building a stage in saga orchestration.
    /// </summary>
    internal class StageConfiguration
    {
        /// <summary>
        /// Gets or sets the stage identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the stage name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the stage description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the step builder for the forward step.
        /// </summary>
        public IInternalStepBuilder StepForwardBuilder { get; set; }

        /// <summary>
        /// Gets or sets the step builder for the compensation step.
        /// </summary>
        public IInternalStepBuilder CompensationBuilder { get; set; }
    }
}
