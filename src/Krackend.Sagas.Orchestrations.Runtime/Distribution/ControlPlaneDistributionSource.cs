namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Represents a control-plane node that a runtime can trust for artifact distribution.
/// </summary>
public sealed class ControlPlaneDistributionSource
{
    /// <summary>
    /// Gets or sets the local source key used by the runtime.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the display name for the control-plane source.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the source control-plane base URI.
    /// </summary>
    public string EndpointBaseUri { get; set; }

    /// <summary>
    /// Gets or sets the runtime node identifier assigned by this control plane.
    /// </summary>
    public string RemoteRuntimeNodeId { get; set; }

    /// <summary>
    /// Gets or sets the client id imported from Design for outbound runtime calls.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the protected client secret imported from Design for outbound runtime calls.
    /// </summary>
    public string ProtectedSecret { get; set; }

    /// <summary>
    /// Gets or sets the credential key id imported from Design.
    /// </summary>
    public string KeyId { get; set; }

    /// <summary>
    /// Gets or sets the requested scopes used when this runtime calls Design.
    /// </summary>
    public string RequestedScopes { get; set; }

    /// <summary>
    /// Gets or sets the renewal skew in seconds used before tokens expire.
    /// </summary>
    public int TokenRefreshSkewSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets a value indicating whether this source is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
