using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion
{
    public class PromotionRequest
    {
        public string ArtifactId { get; set; }

        public JsonNode Payload { get; set; }
    }
}
