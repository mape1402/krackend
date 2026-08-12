namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Resolves a runtime task dispatcher for a task kind.
/// </summary>
public interface IRuntimeTaskDispatcherResolver
{
    /// <summary>
    /// Resolves the dispatcher for a task kind.
    /// </summary>
    /// <param name="taskKind">Task kind declared by Design.</param>
    /// <returns>Dispatcher or null when unsupported.</returns>
    IRuntimeTaskDispatcher Resolve(string taskKind);
}
