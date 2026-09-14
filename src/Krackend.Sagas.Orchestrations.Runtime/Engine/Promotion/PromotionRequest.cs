using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion
{
    /// <summary>
    /// Contains the data required to promote an ingress payload into an orchestration instance.
    /// </summary>
    public class PromotionRequest
    {
        /// <summary>
        /// Gets or sets the runtime artifact id used to resolve the orchestration definition.
        /// </summary>
        public string ArtifactId { get; set; }

        /// <summary>
        /// Gets or sets the business payload that started the orchestration.
        /// </summary>
        public JsonNode Payload { get; set; }

        /// <summary>
        /// Gets or sets the orchestration message metadata received with the trigger.
        /// </summary>
        public OrchestrationMessageMetadata MessageMetadata { get; set; }
    }
}
