using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record CompleteStageDecision(
        Id InstanceId,
        Id StageExecutionId,
        StageArtifact Stage,
        IReadOnlyCollection<StageArtifact> Stages) : IDecision
    {
        public string Kind => "complete-stage";
    }
}
