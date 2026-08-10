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
    /// Represents waiting.
    /// </summary>
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
    /// Represents failed.
    /// </summary>
    Failed
}
