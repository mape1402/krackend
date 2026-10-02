namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

/// <summary>
/// Dispatches orchestration task attempts using the adapter registered for the task kind.
/// </summary>
internal interface ITaskAttemptDispatcher
{
    Task DispatchAsync(TaskAttemptDispatchRequest request, CancellationToken cancellationToken = default);
}
