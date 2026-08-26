namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Represents a client credentials token request between Design and Runtime nodes.
/// </summary>
public sealed class ConnectionTokenRequest
{
    /// <summary>
    /// Gets or sets the grant type. Only client_credentials is supported.
    /// </summary>
    public string GrantType { get; set; } = "client_credentials";

    /// <summary>
    /// Gets or sets the client identifier.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets requested space-separated scopes.
    /// </summary>
    public string Scope { get; set; } = string.Empty;
}
