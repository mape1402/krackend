using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Metadata
{
    internal sealed class DefaultOrchestrationPropagationMetadataStore : IOrchestrationPropagationMetadataStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public OrchestrationPropagationMetadata Load(OrchestrationInstance instance)
        {
            if (instance?.Metadata is null ||
                !instance.Metadata.TryGetValue(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey, out var node) ||
                node is null)
            {
                return new OrchestrationPropagationMetadata();
            }

            var metadata = TryDeserializeEnvelope(node) ?? TryDeserializeItems(node);
            return metadata?.Clone() ?? new OrchestrationPropagationMetadata();
        }

        public void Save(OrchestrationInstance instance, OrchestrationPropagationMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(instance);

            if (metadata is not { HasItems: true })
            {
                instance.Metadata.Remove(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey);
                return;
            }

            instance.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
                JsonSerializer.SerializeToNode(metadata.Clone(), SerializerOptions) ?? new JsonObject();
        }

        private static OrchestrationPropagationMetadata TryDeserializeEnvelope(JsonNode node)
        {
            try
            {
                var metadata = node.Deserialize<OrchestrationPropagationMetadata>(SerializerOptions);
                return metadata?.Items is null ? null : metadata;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static OrchestrationPropagationMetadata TryDeserializeItems(JsonNode node)
        {
            try
            {
                var items = node.Deserialize<Dictionary<string, JsonNode>>(SerializerOptions);
                if (items is null)
                {
                    return null;
                }

                var metadata = new OrchestrationPropagationMetadata();
                foreach (var item in items)
                {
                    metadata.Items[item.Key] = item.Value?.DeepClone();
                }

                return metadata;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
