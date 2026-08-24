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
    /// Gets or sets the key identifier used to sign runtime pull requests and validate incoming pushes.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the configuration reference for the shared signing secret.
    /// </summary>
    public string SecretReference { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this source is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
