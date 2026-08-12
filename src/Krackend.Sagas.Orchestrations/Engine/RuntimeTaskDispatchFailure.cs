using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Carries the values needed to mark a messaging dispatch as failed.
/// </summary>
internal sealed class RuntimeTaskDispatchFailure
{
    /// <summary>
    /// Gets the runtime task execution context.
    /// </summary>
    public required RuntimeTaskExecutionContext Context { get; init; }

    /// <summary>
    /// Gets the task execution being failed.
    /// </summary>
    public required TaskExecution TaskExecution { get; init; }

    /// <summary>
    /// Gets the task execution attempt being failed.
    /// </summary>
    public required TaskExecutionAttempt Attempt { get; init; }

    /// <summary>
    /// Gets the dispatch being failed.
    /// </summary>
    public required TaskDispatch Dispatch { get; init; }

    /// <summary>
    /// Gets the dispatcher failure result.
    /// </summary>
    public required RuntimeTaskDispatchResult DispatchResult { get; init; }
}
