using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads
{
    internal sealed class DefaultOrchestrationPayloadState : IOrchestrationPayloadState
    {
        private const string TriggerPropertyName = "trigger";
        private const string PayloadPropertyName = "payload";
        private const string StagesPropertyName = "stages";
        private const string TasksPropertyName = "tasks";
        private const string RequestPropertyName = "request";
        private const string ResponsePropertyName = "response";
        private const string VariablesPropertyName = "variables";

        public JsonNode CreateInitialPayload(JsonNode triggerPayload)
            => new JsonObject
            {
                [TriggerPropertyName] = new JsonObject
                {
                    [PayloadPropertyName] = triggerPayload?.DeepClone()
                },
                [StagesPropertyName] = new JsonObject(),
                [VariablesPropertyName] = new JsonObject()
            };

        public JsonNode GetDispatchPayload(OrchestrationInstance instance, JsonNode signalPayload)
        {
            var current = instance?.SnapshotPayload;
            if (TryGetTriggerPayload(current, out var triggerPayload))
            {
                return triggerPayload?.DeepClone();
            }

            return (current ?? signalPayload)?.DeepClone();
        }

        public JsonNode ApplyTaskRequestPayload(
            OrchestrationInstance instance,
            string stageKey,
            string taskKey,
            JsonNode requestPayload)
        {
            var root = GetOrCreateRoot(instance?.SnapshotPayload);
            var task = GetOrCreateTask(root, stageKey, taskKey);
            task[RequestPropertyName] = requestPayload?.DeepClone();
            return root;
        }

        public JsonNode ApplyCallbackPayload(
            OrchestrationInstance instance,
            string stageKey,
            string taskKey,
            JsonNode callbackPayload)
        {
            var root = GetOrCreateRoot(instance?.SnapshotPayload);
            var task = GetOrCreateTask(root, stageKey, taskKey);
            task[ResponsePropertyName] = callbackPayload?.DeepClone();
            return root;
        }

        private static JsonObject GetOrCreateRoot(JsonNode current)
        {
            if (current is JsonObject currentObject && currentObject.ContainsKey(TriggerPropertyName))
            {
                return (JsonObject)currentObject.DeepClone();
            }

            return new JsonObject
            {
                [TriggerPropertyName] = new JsonObject
                {
                    [PayloadPropertyName] = current?.DeepClone()
                },
                [StagesPropertyName] = new JsonObject(),
                [VariablesPropertyName] = new JsonObject()
            };
        }

        private static JsonObject GetOrCreateTask(JsonObject root, string stageKey, string taskKey)
        {
            var stages = GetOrCreateObject(root, StagesPropertyName);
            var stage = GetOrCreateObject(stages, stageKey);
            var tasks = GetOrCreateObject(stage, TasksPropertyName);
            return GetOrCreateObject(tasks, taskKey);
        }

        private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
        {
            if (parent[propertyName] is JsonObject existing)
            {
                return existing;
            }

            var created = new JsonObject();
            parent[propertyName] = created;
            return created;
        }

        private static bool TryGetTriggerPayload(JsonNode current, out JsonNode payload)
        {
            payload = null;

            if (current is not JsonObject root ||
                root[TriggerPropertyName] is not JsonObject trigger)
            {
                return false;
            }

            payload = trigger[PayloadPropertyName];
            return true;
        }
    }
}
