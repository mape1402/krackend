using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record CompleteStageDecision(
        Id InstanceId,
        Id StageExecutionId) : IDecision
    {
        public string Kind => "complete-stage";
    }
}
