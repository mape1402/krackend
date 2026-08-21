using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine
{
    /// <summary>
    /// Describes a request to promote an ingress payload into a new orchestration instance.
    /// </summary>
    public class StartIntent
    {
        /// <summary>
        /// Gets the runtime artifact id used to resolve the orchestration definition.
        /// </summary>
        public string ArtifactId { get; init; }

        /// <summary>
        /// Gets the business payload that starts the orchestration.
        /// </summary>
        public JsonNode Payload { get; init; }

        /// <summary>
        /// Gets or sets the transport that received the trigger payload.
        /// </summary>
        public IngressTransport IngressTransport { get; set; }

        /// <summary>
        /// Gets the orchestration message metadata received with the trigger payload.
        /// </summary>
        public OrchestrationMessageMetadata MessageMetadata { get; init; }
    }
}
