namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;

/// <summary>
/// Persists one runtime state transition as an atomic storage operation.
/// </summary>
internal interface IRuntimeStateTransaction
{
    /// <summary>
    /// Executes all state writes and commits them together.
    /// </summary>
    Task ExecuteAsync(Func<CancellationToken, Task> mutation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an internal decision mutation to the active engine batch. When there
    /// is no active batch, the mutation is committed like a normal transition.
    /// </summary>
    Task ExecuteDeferredAsync(Func<CancellationToken, Task> mutation, CancellationToken cancellationToken = default)
        => ExecuteAsync(mutation, cancellationToken);

    /// <summary>
    /// Executes a complete engine activation in one storage transaction while
    /// preserving the save boundaries required by the decision loop.
    /// </summary>
    Task ExecuteBatchAsync(Func<CancellationToken, Task> mutation, CancellationToken cancellationToken = default)
        => ExecuteAsync(mutation, cancellationToken);
}
