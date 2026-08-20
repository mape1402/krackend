using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record CompleteCallbackDecision(
        Id InstanceId,
        Id TaskExecutionId,
        Id DispatchId,
        string Payload) : IDecision
    {
        public string Kind => "complete-callback";
    }
}
