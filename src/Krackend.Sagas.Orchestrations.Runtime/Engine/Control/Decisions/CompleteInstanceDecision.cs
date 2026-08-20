using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record CompleteInstanceDecision(Id InstanceId) : IDecision
    {
        public string Kind => "complete-instance";
    }
}
