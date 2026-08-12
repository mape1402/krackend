namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Detects runtime work that must be resumed outside an active request.
/// </summary>
public interface IRuntimePendingWorkProcessor
{
    /// <summary>
    /// Finds pending runtime work due at the provided instant.
    /// </summary>
    /// <param name="nowUtc">Current UTC instant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending work scan result.</returns>
    Task<RuntimePendingWorkResult> ProcessDueWork(DateTime nowUtc, CancellationToken cancellationToken = default);
}
