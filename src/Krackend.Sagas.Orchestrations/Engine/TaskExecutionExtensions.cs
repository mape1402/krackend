using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Task execution helpers used by the runtime engine.
/// </summary>
internal static class TaskExecutionExtensions
{
    /// <summary>
    /// Reads the failure summary stored on task execution metadata.
    /// </summary>
    /// <param name="taskExecution">Task execution to inspect.</param>
    /// <returns>Failure summary when available.</returns>
    public static string ErrorSummary(this TaskExecution taskExecution)
    {
        if (taskExecution?.Metadata is not null
            && taskExecution.Metadata.TryGetValue("failureReason", out var failureReason))
            return failureReason?.ToString();

        return "Task execution failed.";
    }
}
