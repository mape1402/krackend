namespace Krackend.Sagas.Orchestrations.Client.Serialization;

using System.Text.Json;
using System.Text.Json.Nodes;

internal static class OrchestrationPayloadSerializer
{
    public static JsonNode ToJsonNode(object payload)
        => payload is null ? null : JsonSerializer.SerializeToNode(payload);
}
