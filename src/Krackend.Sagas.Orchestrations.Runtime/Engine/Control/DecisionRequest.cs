using Krackend.Sagas.Orchestrations.Runtime.Metadata;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    public class DecisionRequest
    {
        public string ArtifactId { get; init; }

        public JsonNode Payload { get; init; }

        public InstanceMetadata Metadata { get; init; }
    }
}
