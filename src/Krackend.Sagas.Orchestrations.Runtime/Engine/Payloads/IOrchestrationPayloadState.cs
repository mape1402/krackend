using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads
{
    internal interface IOrchestrationPayloadState
    {
        JsonNode GetDispatchPayload(OrchestrationInstance instance, JsonNode signalPayload);

        JsonNode ApplyCallbackPayload(OrchestrationInstance instance, JsonNode callbackPayload);
    }
}
