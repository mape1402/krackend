namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Represents the configuration data for building a step in saga orchestration.
    /// </summary>
    internal class StepConfiguration
    {
        /// <summary>
        /// Gets or sets the resolve-to destination for the step.
        /// </summary>
        public string ResolveTo { get; set; }

        /// <summary>
        /// Gets or sets the semantic version for the step.
        /// </summary>
        public SemanticVersion Version { get; set; }

        /// <summary>
        /// Gets or sets the number of allowed retries for the step. Default is 1.
        /// </summary>
        public int AllowedRetries { get; set; } = 1;
    }
}
