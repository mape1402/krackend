namespace Krackend.Sagas.Orchestrations.ControlPlane.Api;

/// <summary>
/// Represents a credential issuer request.
/// </summary>
public sealed class CredentialIssuerRequest
{
    /// <summary>
    /// Gets or sets the issuer base URL used in generated credentials.
    /// </summary>
    public string IssuerBaseUrl { get; set; } = string.Empty;
}
