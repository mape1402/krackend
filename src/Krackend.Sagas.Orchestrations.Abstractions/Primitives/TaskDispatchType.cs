namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Describes how task execution is dispatched and awaited by the runtime.
/// </summary>
public enum TaskDispatchType
{
    /// <summary>
    /// Dispatches the task and does not wait for completion.
    /// </summary>
    FireAndForget = 0,

    /// <summary>
    /// Dispatches the task and waits synchronously for completion.
    /// </summary>
    FireAndWait = 1,

    /// <summary>
    /// Dispatches the task and waits for an asynchronous callback to complete it.
    /// </summary>
    FireAndWaitCallback = 2
}
