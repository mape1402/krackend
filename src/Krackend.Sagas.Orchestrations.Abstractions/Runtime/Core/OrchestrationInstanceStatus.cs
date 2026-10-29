namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Represents the orchestration instance status values.
/// </summary>
public enum OrchestrationInstanceStatus
{
    /// <summary>
    /// Represents created.
    /// </summary>
    Created,
    /// <summary>
    /// Represents running.
    /// </summary>
    Running,
    /// <summary>
    /// Represents an instance that has exhausted immediately executable work but is still
    /// accepting a reconciliable late callback before it is dead-lettered.
    /// </summary>
    Idle,
    /// <summary>
    /// Legacy aggregate waiting state. Awaiting a response is represented by task and
    /// attempt state; new runtime writes must use <see cref="Running"/> instead.
    /// </summary>
    [Obsolete("Use Running for an active orchestration. AwaitingResponse belongs to a task attempt.")]
    Waiting,
    /// <summary>
    /// Represents stopped.
    /// </summary>
    Stopped,
    /// <summary>
    /// Represents compensating.
    /// </summary>
    Compensating,
    /// <summary>
    /// Represents compensated.
    /// </summary>
    Compensated,
    /// <summary>
    /// Represents completed.
    /// </summary>
    Completed,
    /// <summary>
    /// Represents completed with errors.
    /// </summary>
    CompletedWithErrors,
    /// <summary>
    /// Represents a recoverable failure waiting for operator action.
    /// </summary>
    DeadLettered,
    /// <summary>
    /// Represents an operator-aborted instance.
    /// </summary>
    Aborted,
    /// <summary>
    /// Represents failed.
    /// </summary>
    Failed
}
