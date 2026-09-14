namespace Krackend.Sagas.Orchestrations.Client.Serialization;

using System.Text.Json.Nodes;

/// <summary>
/// Converts business payload objects into transport-safe orchestration payloads.
/// </summary>
public interface IOrchestrationPayloadSerializer
{
    /// <summary>
    /// Converts a business payload to a JSON node without adding orchestration execution metadata.
    /// </summary>
    /// <param name="payload">Business payload to convert.</param>
    /// <returns>The converted JSON node, or null when the payload is null.</returns>
    JsonNode ToJsonNode(object payload);
}
