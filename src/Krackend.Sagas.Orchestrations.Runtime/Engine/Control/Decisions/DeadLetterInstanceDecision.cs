using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record DeadLetterInstanceDecision(
        Id InstanceId,
        Id? StageExecutionId,
        Id? TaskExecutionId,
        string Reason) : IDecision
    {
        public string Kind => "dead-letter-instance";
    }
}
