namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Represents the task execution status values.
/// </summary>
public enum TaskExecutionStatus
{
    /// <summary>
    /// Represents pending.
    /// </summary>
    Pending,
    /// <summary>
    /// Represents running.
    /// </summary>
    Running,
    /// <summary>
    /// Represents waiting response.
    /// </summary>
    WaitingResponse,
    /// <summary>
    /// Represents retrying.
    /// </summary>
    Retrying,
    /// <summary>
    /// Represents skipped.
    /// </summary>
    Skipped,
    /// <summary>
    /// Represents completed.
    /// </summary>
    Completed,
    /// <summary>
    /// Represents completed with errors.
    /// </summary>
    CompletedWithErrors,
    /// <summary>
    /// Represents failed.
    /// </summary>
    Failed,
    /// <summary>
    /// Represents timed out.
    /// </summary>
    TimedOut,
    /// <summary>
    /// Represents cancelled.
    /// </summary>
    Cancelled,
    /// <summary>
    /// Represents compensated.
    /// </summary>
    Compensated
}
