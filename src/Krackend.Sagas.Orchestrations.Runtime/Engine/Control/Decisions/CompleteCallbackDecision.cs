using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record CompleteCallbackDecision(
        Id InstanceId,
        Id TaskExecutionId,
        Id DispatchId,
        string Payload,
        bool Succeeded,
        string ErrorCode,
        string ErrorMessage,
        string EnvelopePayload) : IDecision
    {
        public string Kind => "complete-callback";
    }
}
