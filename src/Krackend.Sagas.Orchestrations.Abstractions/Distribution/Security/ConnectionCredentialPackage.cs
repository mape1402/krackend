namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Represents a portable credential package exchanged between Design and Runtime nodes.
/// </summary>
public sealed class ConnectionCredentialPackage
{
    /// <summary>
    /// Gets or sets the package schema version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the component that generated the package.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the intended receiver component type.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected synchronization mode.
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL of the issuer.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local node id that generated the package.
    /// </summary>
    public string IssuerNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local node code that generated the package.
    /// </summary>
    public string IssuerNodeCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the remote node id expected to import the package.
    /// </summary>
    public string TargetNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the remote node code expected to import the package.
    /// </summary>
    public string TargetNodeCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the public client identifier.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated client secret. This value is shown once and must be protected by the importing side.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the credential key identifier.
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the space-separated scopes allowed for this credential.
    /// </summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the package was generated.
    /// </summary>
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}
