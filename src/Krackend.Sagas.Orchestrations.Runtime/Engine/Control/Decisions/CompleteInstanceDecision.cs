using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record CompleteInstanceDecision(Id InstanceId) : IDecision
    {
        public string Kind => "complete-instance";

        public OrchestrationInstance Instance { get; init; }

        public OrchestrationDecisionState State { get; init; }
    }
}
