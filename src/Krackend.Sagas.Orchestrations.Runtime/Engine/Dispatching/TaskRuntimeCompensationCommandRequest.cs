namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Describes the state needed to build a compensation command.
/// </summary>
public sealed class TaskRuntimeCompensationCommandRequest
{
    /// <summary>
    /// Gets the orchestration instance being compensated.
    /// </summary>
    public OrchestrationInstance Instance { get; init; }

    /// <summary>
    /// Gets the completed task execution that triggered this compensation.
    /// </summary>
    public TaskExecution SourceTaskExecution { get; init; }

    /// <summary>
    /// Gets the compensation execution record.
    /// </summary>
    public CompensationExecution CompensationExecution { get; init; }

    /// <summary>
    /// Gets the compensation artifact to dispatch.
    /// </summary>
    public CompensationArtifact Compensation { get; init; }

    /// <summary>
    /// Gets the compensation command payload.
    /// </summary>
    public string Payload { get; init; }
}
