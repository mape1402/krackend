using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering
{
    /// <summary>
    /// Represents an ingress work item accepted by the runtime intake buffer.
    /// </summary>
    public class WorkItem
    {
        /// <summary>
        /// Gets the runtime artifact id associated with the ingress configuration.
        /// </summary>
        public string ArtifactId { get; init; }

        /// <summary>
        /// Gets the business payload received by the ingress.
        /// </summary>
        public JsonNode Payload { get; init; }

        /// <summary>
        /// Gets the kind of ingress that produced the work item.
        /// </summary>
        public IngressKind IngressKind { get; init; }

        /// <summary>
        /// Gets or sets the transport that produced the work item.
        /// </summary>
        public IngressTransport IngressTransport { get; set; }

        /// <summary>
        /// Gets the orchestration message metadata received from the transport.
        /// </summary>
        public OrchestrationMessageMetadata MessageMetadata { get; init; }

        /// <summary>
        /// Gets the execution result metadata received from the transport, when the item is a backchannel callback.
        /// </summary>
        public OrchestrationExecutionResultMetadata ExecutionResultMetadata { get; init; }
    }
}
