namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Creates payload contexts from the instance snapshot payload.
/// </summary>
public sealed class DefaultOrchestrationPayloadContextFactory : IOrchestrationPayloadContextFactory
{
    /// <inheritdoc />
    public OrchestrationPayloadContext Create(
        OrchestrationInstance instance,
        string stageKey,
        string taskKey)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var context = instance.SnapshotPayload?.DeepClone();
        return new OrchestrationPayloadContext
        {
            ContextPayload = context,
            TriggerPayload = GetTriggerPayload(context),
            StageKey = stageKey,
            TaskKey = taskKey
        };
    }

    private static JsonNode GetTriggerPayload(JsonNode context)
    {
        if (context is JsonObject root &&
            root["trigger"] is JsonObject trigger &&
            trigger["payload"] is JsonNode payload)
        {
            return payload.DeepClone();
        }

        return context?.DeepClone();
    }
}
