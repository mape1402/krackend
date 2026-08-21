using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine
{
    /// <summary>
    /// Describes a request to continue an existing orchestration instance.
    /// </summary>
    public class ForwardIntent
    {
        /// <summary>
        /// Gets the runtime artifact id used to resolve the orchestration definition.
        /// </summary>
        public string ArtifactId { get; init; }

        /// <summary>
        /// Gets the business payload received from the backchannel.
        /// </summary>
        public JsonNode Payload { get; init; }

        /// <summary>
        /// Gets or sets the transport that received the callback payload.
        /// </summary>
        public IngressTransport IngressTransport { get; set; }

        /// <summary>
        /// Gets the orchestration message metadata received with the callback.
        /// </summary>
        public OrchestrationMessageMetadata MessageMetadata { get; init; }

        /// <summary>
        /// Gets the execution result metadata received with the callback.
        /// </summary>
        public OrchestrationExecutionResultMetadata ExecutionResultMetadata { get; init; }
    }
}
