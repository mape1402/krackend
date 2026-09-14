namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Builds cache keys used by Design and Runtime connection tokens.
/// </summary>
public sealed class ConnectionTokenCacheKeyBuilder
{
    /// <summary>
    /// Builds the server-side opaque token cache key.
    /// </summary>
    /// <param name="issuer">Component issuing the token.</param>
    /// <param name="tokenHash">Hashed token value.</param>
    /// <returns>Cache key.</returns>
    public string BuildIssuedTokenKey(string issuer, string tokenHash)
        => $"krackend:orchestrations:connections:{issuer}:issued:{tokenHash}";

    /// <summary>
    /// Builds the positive validation cache key.
    /// </summary>
    /// <param name="issuer">Component validating the token.</param>
    /// <param name="tokenHash">Hashed token value.</param>
    /// <returns>Cache key.</returns>
    public string BuildValidationKey(string issuer, string tokenHash)
        => $"krackend:orchestrations:connections:{issuer}:validation:{tokenHash}";

    /// <summary>
    /// Builds the consumer-side token cache key.
    /// </summary>
    /// <param name="consumer">Component consuming the token.</param>
    /// <param name="nodeId">Local node id.</param>
    /// <param name="scopes">Requested scopes.</param>
    /// <returns>Cache key.</returns>
    public string BuildConsumerTokenKey(string consumer, string nodeId, string scopes)
        => $"krackend:orchestrations:connections:{consumer}:consumer:{nodeId}:{scopes}";
}
