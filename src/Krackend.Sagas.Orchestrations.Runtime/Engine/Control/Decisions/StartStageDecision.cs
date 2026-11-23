using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record StartStageDecision(
        Id InstanceId,
        StageArtifact Stage,
        string Payload) : IDecision
    {
        public string Kind => "start-stage";

        public OrchestrationInstance Instance { get; init; }

        public OrchestrationDecisionState State { get; init; }

        public IReadOnlyCollection<MetadataDescriptorArtifact> MetadataDescriptors { get; init; } = Array.Empty<MetadataDescriptorArtifact>();
    }
}
