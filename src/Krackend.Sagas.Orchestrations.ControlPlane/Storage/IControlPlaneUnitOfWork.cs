namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage;

/// <summary>
/// Defines the transaction boundary used by control-plane application operations.
/// </summary>
public interface IControlPlaneUnitOfWork
{
    /// <summary>
    /// Executes related control-plane writes as one atomic operation when the configured provider supports transactions.
    /// </summary>
    /// <param name="operation">Writes to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists all pending control-plane changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The number of state entries written to storage.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
