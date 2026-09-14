namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Computes stable hashes for opaque access token cache keys.
/// </summary>
public interface ITokenHashService
{
    /// <summary>
    /// Computes a SHA-256 Base64Url hash for a token.
    /// </summary>
    /// <param name="token">Token to hash.</param>
    /// <returns>Token hash.</returns>
    string HashToken(string token);
}
