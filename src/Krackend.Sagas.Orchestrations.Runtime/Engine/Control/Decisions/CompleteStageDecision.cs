using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record CompleteStageDecision(
        Id InstanceId,
        Id StageExecutionId,
        StageArtifact Stage,
        IReadOnlyCollection<StageArtifact> Stages) : IDecision
    {
        public string Kind => "complete-stage";

        public OrchestrationInstance Instance { get; init; }

        public StageExecution StageExecution { get; init; }

        public OrchestrationDecisionState State { get; init; }

        public IReadOnlyCollection<MetadataDescriptorArtifact> MetadataDescriptors { get; init; } = Array.Empty<MetadataDescriptorArtifact>();
    }
}
