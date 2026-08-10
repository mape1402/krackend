using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Source values used to build a messaging dispatch command.
/// </summary>
internal sealed class MessagingDispatchCommandSource
{
    /// <summary>
    /// Gets the orchestration instance.
    /// </summary>
    public required OrchestrationInstance Instance { get; init; }

    /// <summary>
    /// Gets the orchestration artifact version.
    /// </summary>
    public required string OrchestrationVersion { get; init; }

    /// <summary>
    /// Gets the current stage execution.
    /// </summary>
    public required StageExecution StageExecution { get; init; }

    /// <summary>
    /// Gets the current task execution.
    /// </summary>
    public required TaskExecution TaskExecution { get; init; }

    /// <summary>
    /// Gets the current task attempt.
    /// </summary>
    public required TaskExecutionAttempt Attempt { get; init; }

    /// <summary>
    /// Gets the persisted dispatch entry.
    /// </summary>
    public required TaskDispatch Dispatch { get; init; }

    /// <summary>
    /// Gets the runtime task definition.
    /// </summary>
    public required RuntimeTaskDocument Task { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the attempt started.
    /// </summary>
    public DateTime StartedOnUtc { get; init; }
}
