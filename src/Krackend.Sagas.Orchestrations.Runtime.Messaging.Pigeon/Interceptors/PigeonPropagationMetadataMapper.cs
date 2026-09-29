using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using global::Pigeon.Messaging.Consuming.Dispatching;
using global::Pigeon.Messaging.Producing;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors
{
    internal sealed class PigeonPropagationMetadataMapper
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private static readonly FieldInfo ConsumeMetadataField = typeof(ConsumeContext).GetField(
            "_metadata",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public OrchestrationPropagationMetadata Capture(ConsumeContext context)
        {
            var metadata = new OrchestrationPropagationMetadata();
            MergeEnvelope(context, metadata);
            MergeObjectMetadata(context, metadata);
            MergeRawMetadata(context, metadata);
            return metadata;
        }

        public void Attach(PublishContext context, OrchestrationPropagationMetadata metadata)
        {
            if (metadata is not { HasItems: true })
            {
                return;
            }

            foreach (var item in metadata.Clone().Items)
            {
                if (IsReserved(item.Key))
                {
                    continue;
                }

                context.AddMetadata(item.Key, item.Value?.DeepClone());
            }
        }

        private static void MergeEnvelope(ConsumeContext context, OrchestrationPropagationMetadata target)
        {
            try
            {
                var envelope = context.GetMetadata<OrchestrationPropagationMetadata>(
                    OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey);
                Merge(target, envelope);
                return;
            }
            catch
            {
            }

            try
            {
                var items = context.GetMetadata<Dictionary<string, JsonNode>>(
                    OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey);
                Merge(target, items);
            }
            catch
            {
            }
        }

        private static void MergeObjectMetadata(ConsumeContext context, OrchestrationPropagationMetadata target)
        {
            if (ConsumeMetadataField?.GetValue(context) is not IEnumerable metadata)
            {
                return;
            }

            foreach (var entry in metadata)
            {
                var keyProperty = entry.GetType().GetProperty("Key");
                var valueProperty = entry.GetType().GetProperty("Value");
                var key = keyProperty?.GetValue(entry) as string;
                if (string.IsNullOrWhiteSpace(key) || IsReserved(key))
                {
                    continue;
                }

                target.Items[key] = ConvertValue(valueProperty?.GetValue(entry));
            }
        }

        private static void MergeRawMetadata(ConsumeContext context, OrchestrationPropagationMetadata target)
        {
            if (context.RawMetadata is null)
            {
                return;
            }

            foreach (var item in context.RawMetadata)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || IsReserved(item.Key) || target.Items.ContainsKey(item.Key))
                {
                    continue;
                }

                target.Items[item.Key] = ConvertValue(item.Value);
            }
        }

        private static void Merge(OrchestrationPropagationMetadata target, OrchestrationPropagationMetadata source)
            => Merge(target, source?.Items);

        private static void Merge(OrchestrationPropagationMetadata target, IDictionary<string, JsonNode> source)
        {
            if (source is null)
            {
                return;
            }

            foreach (var item in source)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || IsReserved(item.Key))
                {
                    continue;
                }

                target.Items[item.Key] = item.Value?.DeepClone();
            }
        }

        private static JsonNode ConvertValue(object value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is JsonNode node)
            {
                return node.DeepClone();
            }

            if (value is JsonElement element)
            {
                return JsonNode.Parse(element.GetRawText());
            }

            if (value is string text)
            {
                return TryParseJson(text) ?? JsonValue.Create(text);
            }

            try
            {
                return JsonSerializer.SerializeToNode(value, SerializerOptions);
            }
            catch
            {
                return JsonValue.Create(value.ToString());
            }
        }

        private static JsonNode TryParseJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                return JsonNode.Parse(value);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static bool IsReserved(string key)
            => !string.Equals(key, OrchestrationMetadataConstants.TriggerMetadataKey, StringComparison.Ordinal) &&
                key.StartsWith("Krackend.Sagas.Orchestrations.", StringComparison.Ordinal);
    }
}
