namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Represents the stage execution status values.
/// </summary>
public enum StageExecutionStatus
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
    Failed
}
