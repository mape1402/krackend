using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

/// <summary>
/// Deduplicates the immutable trigger payload inside the persisted execution context.
/// Context consumers always receive the fully expanded JSON tree.
/// </summary>
internal static class OrchestrationPayloadReferences
{
    private const string ReferenceProperty = "$krackend.runtime.ref";
    private const string TriggerReference = "trigger.payload";

    internal static JsonNode Compact(JsonObject root, JsonNode value)
    {
        var trigger = root["trigger"]?["payload"];
        return CompactNode(value, trigger);
    }

    internal static JsonNode Expand(JsonNode value)
    {
        if (value is not JsonObject root) return value?.DeepClone();
        var trigger = root["trigger"]?["payload"]?.DeepClone();
        return ExpandNode(root, trigger);
    }

    private static JsonNode CompactNode(JsonNode value, JsonNode trigger)
    {
        if (value is null) return null;
        if (trigger is not null && JsonNode.DeepEquals(value, trigger))
            return new JsonObject { [ReferenceProperty] = TriggerReference };

        if (value is JsonObject sourceObject)
        {
            var result = new JsonObject();
            foreach (var pair in sourceObject)
                result[pair.Key] = CompactNode(pair.Value, trigger);
            return result;
        }

        if (value is JsonArray sourceArray)
        {
            var result = new JsonArray();
            foreach (var item in sourceArray)
                result.Add(CompactNode(item, trigger));
            return result;
        }

        return value.DeepClone();
    }

    private static JsonNode ExpandNode(JsonNode value, JsonNode trigger)
    {
        if (value is null) return null;
        if (value is JsonObject sourceObject)
        {
            if (sourceObject.Count == 1 &&
                sourceObject[ReferenceProperty]?.GetValue<string>() == TriggerReference)
                return trigger?.DeepClone();

            var result = new JsonObject();
            foreach (var pair in sourceObject)
                result[pair.Key] = ExpandNode(pair.Value, trigger);
            return result;
        }

        if (value is JsonArray sourceArray)
        {
            var result = new JsonArray();
            foreach (var item in sourceArray)
                result.Add(ExpandNode(item, trigger));
            return result;
        }

        return value.DeepClone();
    }
}
