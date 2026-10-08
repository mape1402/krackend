using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching
{
    /// <summary>
    /// Builds the transport-agnostic metadata snapshot associated with a remote command dispatch.
    /// </summary>
    public static class RemoteCommandMetadataBuilder
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Builds the metadata snapshot that should be persisted with the task dispatch.
        /// </summary>
        /// <param name="messageMetadata">The orchestration message metadata attached to the command.</param>
        /// <param name="propagationMetadata">The transversal metadata propagated through the orchestration.</param>
        /// <returns>A cloned metadata dictionary ready to persist.</returns>
        public static Dictionary<string, JsonNode> Build(
            OrchestrationMessageMetadata messageMetadata,
            OrchestrationPropagationMetadata propagationMetadata)
        {
            var metadata = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

            if (HasMessageMetadata(messageMetadata))
            {
                metadata[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey] =
                    JsonSerializer.SerializeToNode(messageMetadata, SerializerOptions) ?? new JsonObject();
            }

            if (propagationMetadata is not { HasItems: true })
            {
                return metadata;
            }

            foreach (var item in propagationMetadata.Clone().Items)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || IsReserved(item.Key))
                {
                    continue;
                }

                metadata[item.Key] = item.Value?.DeepClone();
            }

            return metadata;
        }

        /// <summary>
        /// Determines whether a key is owned by Krackend runtime internals.
        /// </summary>
        /// <param name="key">Metadata key.</param>
        /// <returns><c>true</c> when the key is reserved for Krackend.</returns>
        public static bool IsReserved(string key)
            => !IsAllowedKrackendPropagationKey(key) &&
                !string.IsNullOrWhiteSpace(key) &&
                key.StartsWith("Krackend.Sagas.Orchestrations.", StringComparison.Ordinal);

        private static bool IsAllowedKrackendPropagationKey(string key)
            => string.Equals(key, OrchestrationMetadataConstants.TriggerMetadataKey, StringComparison.Ordinal) ||
                string.Equals(key, OrchestrationMetadataConstants.OriginMetadataKey, StringComparison.Ordinal);

        private static bool HasMessageMetadata(OrchestrationMessageMetadata metadata)
            => !string.IsNullOrWhiteSpace(metadata?.SagaId)
                || !string.IsNullOrWhiteSpace(metadata?.OrchestrationInstanceId)
                || !string.IsNullOrWhiteSpace(metadata?.CurrentStage)
                || metadata?.CurrentTasks is { Length: > 0 }
                || !string.IsNullOrWhiteSpace(metadata?.CorrelationId)
                || !string.IsNullOrWhiteSpace(metadata?.TaskExecutionId)
                || !string.IsNullOrWhiteSpace(metadata?.DispatchId)
                || metadata?.Attempt > 0
                || metadata?.ReplyAddress is not null;
    }
}
