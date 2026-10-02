namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

internal sealed class TaskAttemptDispatchRequest
{
    public TaskAttemptDispatchKind Kind { get; init; }

    public OrchestrationInstance Instance { get; init; }

    public Id StageExecutionId { get; init; }

    public string StageKey { get; init; }

    public TaskArtifact Task { get; init; }

    public ExecutionPolicyArtifact OrchestrationExecutionPolicy { get; init; } = ExecutionPolicyArtifact.Empty;

    public ExecutionPolicyArtifact StageExecutionPolicy { get; init; } = ExecutionPolicyArtifact.Empty;

    public TaskExecution TaskExecution { get; init; }

    public string Payload { get; init; }

    public IReadOnlyCollection<MetadataDescriptorArtifact> MetadataDescriptors { get; init; } = Array.Empty<MetadataDescriptorArtifact>();

    public DateTime NowUtc { get; init; }

    public DateTimeOffset? ScheduledOnUtc { get; init; }
}
