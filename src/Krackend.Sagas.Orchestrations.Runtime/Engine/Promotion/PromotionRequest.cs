using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion
{
    public class PromotionRequest
    {
        public string ArtifactId { get; set; }

        public JsonNode Payload { get; set; }

        public InstanceMetadata Metadata { get; set; }
    }
}
