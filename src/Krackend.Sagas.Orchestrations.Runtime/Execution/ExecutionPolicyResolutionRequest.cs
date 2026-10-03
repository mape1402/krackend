namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;

internal sealed class ExecutionPolicyResolutionRequest
{
    public required string StageKey { get; init; }

    public required TaskArtifact Task { get; init; }

    public ExecutionPolicyArtifact OrchestrationPolicy { get; init; } = ExecutionPolicyArtifact.Empty;

    public ExecutionPolicyArtifact StagePolicy { get; init; } = ExecutionPolicyArtifact.Empty;
}

