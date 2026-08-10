namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the task execution mode values.
/// </summary>
public enum TaskExecutionMode
{
    /// <summary>
    /// Represents sequential.
    /// </summary>
    Sequential,
    /// <summary>
    /// Represents parallel.
    /// </summary>
    Parallel
}
