using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record DispatchTaskDecision(
        Id InstanceId,
        Id StageExecutionId,
        string StageKey,
        TaskArtifact Task,
        string Payload) : IDecision
    {
        public string Kind => "dispatch-task";

        public OrchestrationInstance Instance { get; init; }

        public OrchestrationDecisionState State { get; init; }

        public IReadOnlyCollection<MetadataDescriptorArtifact> MetadataDescriptors { get; init; } = Array.Empty<MetadataDescriptorArtifact>();

        public ExecutionPolicyArtifact OrchestrationExecutionPolicy { get; init; } = ExecutionPolicyArtifact.Empty;

        public ExecutionPolicyArtifact StageExecutionPolicy { get; init; } = ExecutionPolicyArtifact.Empty;

    }
}
