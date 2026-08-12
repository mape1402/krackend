using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Minimal runtime payload transformer.
/// </summary>
public sealed class RuntimePayloadTransformer : IRuntimePayloadTransformer
{
    /// <inheritdoc/>
    public RuntimePayloadTransformation Transform(JsonObject transformation, JsonNode payload)
    {
        var clone = payload?.DeepClone() ?? new JsonObject();
        if (transformation is null || transformation.Count == 0)
            return new RuntimePayloadTransformation { Payload = clone };

        var engine = ReadEngine(transformation);
        if (string.IsNullOrWhiteSpace(engine) || string.Equals(engine, "DSL", StringComparison.OrdinalIgnoreCase) || engine == "0")
            return new RuntimePayloadTransformation
            {
                Payload = clone,
                WasTransformed = true,
                Engine = string.IsNullOrWhiteSpace(engine) ? "DSL" : engine
            };

        throw new NotSupportedException($"Transformation engine '{engine}' is not supported by the runtime transformer yet.");
    }

    private static string ReadEngine(JsonObject transformation)
    {
        var node = transformation["Engine"] ?? transformation["engine"];
        if (node is null)
            return string.Empty;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text ?? string.Empty;

            if (value.TryGetValue<int>(out var number))
                return number == 0 ? "DSL" : number.ToString();
        }

        return node.ToString();
    }
}
