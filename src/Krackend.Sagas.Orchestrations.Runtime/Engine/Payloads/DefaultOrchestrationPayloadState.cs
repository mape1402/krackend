using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads
{
    internal sealed class DefaultOrchestrationPayloadState : IOrchestrationPayloadState
    {
        public JsonNode GetDispatchPayload(OrchestrationInstance instance, JsonNode signalPayload)
            => (instance?.SnapshotPayload ?? signalPayload)?.DeepClone();

        public JsonNode ApplyCallbackPayload(OrchestrationInstance instance, JsonNode callbackPayload)
        {
            var current = instance?.SnapshotPayload;
            if (callbackPayload is null)
            {
                return current?.DeepClone();
            }

            if (current is JsonObject currentObject && callbackPayload is JsonObject callbackObject)
            {
                var merged = (JsonObject)currentObject.DeepClone();
                foreach (var property in callbackObject)
                {
                    merged[property.Key] = property.Value?.DeepClone();
                }

                return merged;
            }

            return callbackPayload.DeepClone();
        }
    }
}
