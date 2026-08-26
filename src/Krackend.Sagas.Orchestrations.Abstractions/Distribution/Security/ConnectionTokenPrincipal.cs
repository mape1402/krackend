namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Represents an authenticated node principal created from a bearer token.
/// </summary>
public sealed class ConnectionTokenPrincipal
{
    /// <summary>
    /// Gets or sets the local node id.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local node key.
    /// </summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authenticated client id.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the credential key id.
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets granted scopes.
    /// </summary>
    public IReadOnlyCollection<ArtifactDeliveryScope> Scopes { get; set; } = Array.Empty<ArtifactDeliveryScope>();
}
