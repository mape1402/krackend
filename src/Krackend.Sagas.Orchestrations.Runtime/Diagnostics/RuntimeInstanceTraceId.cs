using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>Reads the trigger trace identifier from an already loaded instance.</summary>
public static class RuntimeInstanceTraceId
{
    /// <summary>Returns the trace identifier, or null when no trace was recorded.</summary>
    public static string Get(OrchestrationInstance instance)
    {
        var metadata = instance?.Metadata;
        if (metadata is null) return null;
        if (metadata.TryGetValue("TriggerTraceId", out var direct) && direct is JsonValue value && value.TryGetValue<string>(out var trace) && !string.IsNullOrWhiteSpace(trace)) return trace;
        return metadata.TryGetValue(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey, out var propagation)
            ? FindTrace(propagation) : null;
    }

    private static string FindTrace(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                if (string.Equals(pair.Key, "TraceId", StringComparison.OrdinalIgnoreCase) && pair.Value is JsonValue value && value.TryGetValue<string>(out var trace) && !string.IsNullOrWhiteSpace(trace)) return trace;
                var nested = FindTrace(pair.Value);
                if (nested is not null) return nested;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var nested = FindTrace(item);
                if (nested is not null) return nested;
            }
        }
        return null;
    }
}
