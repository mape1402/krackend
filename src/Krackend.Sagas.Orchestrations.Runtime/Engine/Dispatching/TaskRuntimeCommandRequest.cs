namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Describes the state needed to build a task attempt command.
/// </summary>
public sealed class TaskRuntimeCommandRequest
{
    /// <summary>
    /// Gets the orchestration instance that owns the attempt.
    /// </summary>
    public OrchestrationInstance Instance { get; init; }

    /// <summary>
    /// Gets the stage execution id that owns the task execution.
    /// </summary>
    public Id StageExecutionId { get; init; }

    /// <summary>
    /// Gets the stage key that owns the task.
    /// </summary>
    public string StageKey { get; init; }

    /// <summary>
    /// Gets the task artifact being dispatched.
    /// </summary>
    public TaskArtifact Task { get; init; }

    /// <summary>
    /// Gets the task execution being attempted.
    /// </summary>
    public TaskExecution TaskExecution { get; init; }

    /// <summary>
    /// Gets the current task execution attempt.
    /// </summary>
    public TaskExecutionAttempt Attempt { get; init; }

    /// <summary>
    /// Gets the dispatch record associated with the attempt.
    /// </summary>
    public TaskDispatch Dispatch { get; init; }

    /// <summary>
    /// Gets the prepared command payload.
    /// </summary>
    public string Payload { get; init; }
}
