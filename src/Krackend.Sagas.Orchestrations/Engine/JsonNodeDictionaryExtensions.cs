using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

internal static class JsonNodeDictionaryExtensions
{
    public static JsonObject ToJsonObject(this IReadOnlyDictionary<string, JsonNode> values)
    {
        var obj = new JsonObject();
        if (values is null)
            return obj;

        foreach (var (key, value) in values)
        {
            obj[key] = value?.DeepClone();
        }

        return obj;
    }
}
