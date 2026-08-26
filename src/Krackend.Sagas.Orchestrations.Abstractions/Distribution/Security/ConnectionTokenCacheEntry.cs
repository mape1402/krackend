namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Represents the cached server-side data associated with an opaque access token.
/// </summary>
public sealed class ConnectionTokenCacheEntry
{
    /// <summary>
    /// Gets or sets the local node id associated with the token.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local node key associated with the token.
    /// </summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client id used to issue the token.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the credential key id used to issue the token.
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the space-separated scopes granted to the token.
    /// </summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the token expires.
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }
}
