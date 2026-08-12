using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Transforms runtime payloads using transformation configuration promoted by Design.
/// </summary>
public interface IRuntimePayloadTransformer
{
    /// <summary>
    /// Transforms a payload.
    /// </summary>
    /// <param name="transformation">Transformation artifact fragment.</param>
    /// <param name="payload">Source payload.</param>
    /// <returns>Transformation result.</returns>
    RuntimePayloadTransformation Transform(JsonObject transformation, JsonNode payload);
}
