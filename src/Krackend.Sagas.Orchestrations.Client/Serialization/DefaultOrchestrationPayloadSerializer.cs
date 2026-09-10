namespace Krackend.Sagas.Orchestrations.Client.Serialization;

using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed class DefaultOrchestrationPayloadSerializer : IOrchestrationPayloadSerializer
{
    public JsonNode ToJsonNode(object payload)
        => payload is null ? null : JsonSerializer.SerializeToNode(payload);
}
