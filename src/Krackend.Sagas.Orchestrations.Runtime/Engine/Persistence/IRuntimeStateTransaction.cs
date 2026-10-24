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
}
