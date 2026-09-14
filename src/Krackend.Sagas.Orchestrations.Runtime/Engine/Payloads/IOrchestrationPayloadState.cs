using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads
{
    internal interface IOrchestrationPayloadState
    {
        JsonNode CreateInitialPayload(JsonNode triggerPayload);

        JsonNode GetDispatchPayload(OrchestrationInstance instance, JsonNode signalPayload);

        JsonNode ApplyTaskRequestPayload(
            OrchestrationInstance instance,
            string stageKey,
            string taskKey,
            JsonNode requestPayload);

        JsonNode ApplyCallbackPayload(
            OrchestrationInstance instance,
            string stageKey,
            string taskKey,
            JsonNode callbackPayload);
    }
}
