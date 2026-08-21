using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    /// <summary>
    /// Contains the runtime state needed to decide the next orchestration steps.
    /// </summary>
    public class DecisionRequest
    {
        /// <summary>
        /// Gets the runtime artifact id used to resolve the orchestration definition.
        /// </summary>
        public string ArtifactId { get; init; }

        /// <summary>
        /// Gets the business payload available to the current decision cycle.
        /// </summary>
        public JsonNode Payload { get; init; }

        /// <summary>
        /// Gets the orchestration message metadata associated with the current signal.
        /// </summary>
        public OrchestrationMessageMetadata MessageMetadata { get; init; }

        /// <summary>
        /// Gets the execution result metadata associated with a backchannel callback.
        /// </summary>
        public OrchestrationExecutionResultMetadata ExecutionResultMetadata { get; init; }
    }
}
