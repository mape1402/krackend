namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Represents the configuration data for building a roadmap in saga orchestration.
    /// </summary>
    internal class RoadmapConfiguration
    {
        /// <summary>
        /// Gets or sets the orchestration key.
        /// </summary>
        public string OrchestrationKey { get; set; }

        /// <summary>
        /// Gets or sets the orchestration name.
        /// </summary>
        public string OrchestrationName { get; set; }

        /// <summary>
        /// Gets or sets the description of the roadmap.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the event topic.
        /// </summary>
        public string EventTopic { get; set; }

        /// <summary>
        /// Gets or sets the event topic version.
        /// </summary>
        public SemanticVersion EventTopicVersion { get; set; }

        /// <summary>
        /// Gets or sets the orchestration topic.
        /// </summary>
        public string OrchestrationTopic { get; set; }

        /// <summary>
        /// Gets or sets the orchestration topic version.
        /// </summary>
        public SemanticVersion OrchestrationTopicVersion { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the roadmap is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets the list of stage builders for the roadmap.
        /// </summary>
        public IList<IInternalStageBuilder> StageBuilders { get; } = new List<IInternalStageBuilder>();
    }
}
