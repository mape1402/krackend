using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record RetryDecision(
        Id InstanceId,
        Id StageExecutionId,
        Id TaskExecutionId,
        string StageKey,
        TaskArtifact Task,
        string Payload) : IDecision
    {
        public string Kind => "retry-task";

        public IReadOnlyCollection<MetadataDescriptorArtifact> MetadataDescriptors { get; init; } = Array.Empty<MetadataDescriptorArtifact>();

        public ExecutionPolicyArtifact OrchestrationExecutionPolicy { get; init; } = ExecutionPolicyArtifact.Empty;

        public ExecutionPolicyArtifact StageExecutionPolicy { get; init; } = ExecutionPolicyArtifact.Empty;
    }
}
