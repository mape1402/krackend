namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Tracks request nonces to reject replayed artifact delivery requests.
/// </summary>
public interface IArtifactDeliveryNonceStore
{
    /// <summary>
    /// Stores a nonce if it has not been seen before.
    /// </summary>
    /// <param name="keyId">Signing key identifier.</param>
    /// <param name="nonce">Request nonce.</param>
    /// <param name="expiresAt">Expiration time for the nonce.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns><c>true</c> when the nonce was stored; otherwise <c>false</c>.</returns>
    Task<bool> TryStore(string keyId, string nonce, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
}
