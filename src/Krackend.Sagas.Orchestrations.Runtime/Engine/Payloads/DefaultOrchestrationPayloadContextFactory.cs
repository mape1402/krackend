namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;

/// <summary>
/// Creates payload contexts from the instance snapshot payload.
/// </summary>
public sealed class DefaultOrchestrationPayloadContextFactory : IOrchestrationPayloadContextFactory
{
    private readonly IOrchestrationPropagationMetadataStore _metadataStore = new DefaultOrchestrationPropagationMetadataStore();

    /// <inheritdoc />
    public OrchestrationPayloadContext Create(
        OrchestrationInstance instance,
        string stageKey,
        string taskKey,
        IReadOnlyCollection<MetadataDescriptorArtifact> metadataDescriptors = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var context = instance.SnapshotPayload?.DeepClone();
        return new OrchestrationPayloadContext
        {
            ContextPayload = context,
            TriggerPayload = GetTriggerPayload(context),
            MetadataPayload = GetMetadataPayload(_metadataStore.Load(instance), metadataDescriptors),
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

    private static JsonNode GetMetadataPayload(
        OrchestrationPropagationMetadata metadata,
        IReadOnlyCollection<MetadataDescriptorArtifact> descriptors)
    {
        var payload = new JsonObject();
        if (metadata?.Items is not null)
        {
            foreach (var item in metadata.Items)
            {
                if (string.Equals(item.Key, OrchestrationMetadataConstants.LegacyTriggerMetadataKey, StringComparison.Ordinal))
                {
                    continue;
                }

                payload[item.Key] = item.Value?.DeepClone();
            }
        }

        if (!payload.ContainsKey(OrchestrationMetadataConstants.TriggerMetadataKey))
        {
            payload[OrchestrationMetadataConstants.TriggerMetadataKey] =
                TryResolveMetadataItem(metadata?.Items, OrchestrationMetadataConstants.LegacyTriggerMetadataKey, out var legacyTrigger)
                    ? legacyTrigger?.DeepClone()
                    : new OrchestrationTriggerMetadata().ToJson();
        }

        foreach (var descriptor in descriptors ?? [])
        {
            if (string.IsNullOrWhiteSpace(descriptor?.Key))
            {
                continue;
            }

            var sourceKey = string.IsNullOrWhiteSpace(descriptor.SourceKey)
                ? descriptor.Key
                : descriptor.SourceKey;
            if (!TryResolveMetadataItem(metadata.Items, sourceKey, out var value))
            {
                continue;
            }

            payload[descriptor.Key] = value?.DeepClone();
        }

        return payload;
    }

    private static bool TryResolveMetadataItem(
        IReadOnlyDictionary<string, JsonNode> items,
        string sourceKey,
        out JsonNode value)
    {
        value = null;
        if (items is null || string.IsNullOrWhiteSpace(sourceKey))
        {
            return false;
        }

        if (items.TryGetValue(sourceKey, out value))
        {
            return true;
        }

        var matches = items
            .Where(item => string.Equals(item.Key, sourceKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length == 0)
        {
            return false;
        }

        if (matches.Length > 1)
        {
            var keys = string.Join(", ", matches.Select(item => $"'{item.Key}'"));
            throw new InvalidOperationException(
                $"Incoming metadata key '{sourceKey}' is ambiguous. Matching keys: {keys}.");
        }

        value = matches[0].Value;
        return true;
    }
}
