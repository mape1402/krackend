namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage;

/// <summary>
/// Defines the transaction boundary used by control-plane application operations.
/// </summary>
public interface IControlPlaneUnitOfWork
{
    /// <summary>
    /// Persists all pending control-plane changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The number of state entries written to storage.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
