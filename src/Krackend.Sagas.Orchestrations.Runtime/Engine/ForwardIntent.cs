using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine
{
    public class ForwardIntent
    {
        public string ArtifactId { get; init; }

        public JsonNode Payload { get; init; }

        public IngressTransport IngressTransport { get; set; }

        public InstanceMetadata Metadata { get; init; }
    }
}
