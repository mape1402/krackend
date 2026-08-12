namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Dispatches a runtime task through a task-kind-specific integration adapter.
/// </summary>
public interface IRuntimeTaskDispatcher
{
    /// <summary>
    /// Determines whether this dispatcher supports the supplied task kind.
    /// </summary>
    /// <param name="taskKind">Task kind declared by the promoted artifact.</param>
    /// <returns>True when the dispatcher can handle the task.</returns>
    bool CanDispatch(string taskKind);

    /// <summary>
    /// Dispatches the runtime task command.
    /// </summary>
    /// <param name="request">Dispatch request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dispatch result.</returns>
    Task<RuntimeTaskDispatchResult> Dispatch(
        RuntimeTaskDispatchRequest request,
        CancellationToken cancellationToken = default);
}
