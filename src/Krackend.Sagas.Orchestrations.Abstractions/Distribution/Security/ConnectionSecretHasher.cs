namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Hashes and verifies long-lived connection secrets.
/// </summary>
public interface IConnectionSecretHasher
{
    /// <summary>
    /// Hashes one secret using a non-reversible format.
    /// </summary>
    /// <param name="secret">Plain secret to hash.</param>
    /// <returns>Persistable secret hash.</returns>
    string HashSecret(string secret);

    /// <summary>
    /// Verifies a plain secret against a stored hash.
    /// </summary>
    /// <param name="secret">Plain secret to verify.</param>
    /// <param name="storedHash">Persisted hash.</param>
    /// <returns><c>true</c> when the secret matches.</returns>
    bool VerifySecret(string secret, string storedHash);
}
