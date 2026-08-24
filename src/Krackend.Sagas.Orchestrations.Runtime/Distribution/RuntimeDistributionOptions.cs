namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Configures runtime artifact distribution sources.
/// </summary>
public sealed class RuntimeDistributionOptions
{
    /// <summary>
    /// Gets or sets the registered control-plane sources.
    /// </summary>
    public List<ControlPlaneDistributionSource> ControlPlanes { get; set; } = [];
}
