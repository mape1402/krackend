using Krackend.Sagas.Orchestration.Controller.Shared;
using Pigeon.Messaging.Contracts;

namespace Krackend.Sagas.Orchestration.Controller.Configuration.Metadata
{
    /// <summary>
    /// Represents the metadata for a step in saga orchestration.
    /// </summary>
    public class StepMetadata
    {
        /// <summary>
        /// Gets or sets the destination to resolve to for this step.
        /// </summary>
        public string ResolveTo { get; init; }

        /// <summary>
        /// Gets or sets the semantic version for this step.
        /// </summary>
        public SemanticVersion Version { get; set; }

        /// <summary>
        /// Gets or sets the number of allowed retries for this step.
        /// </summary>
        public int AllowedRetries { get; init; }

        /// <summary>
        /// Gets or sets the step execution details.
        /// </summary>
        public IStep StepExecution { get; init; }
    }
}
