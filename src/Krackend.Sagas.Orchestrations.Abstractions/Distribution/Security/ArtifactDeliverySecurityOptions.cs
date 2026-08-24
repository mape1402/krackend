namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Configures authentication settings for artifact delivery requests.
/// </summary>
public sealed class ArtifactDeliverySecurityOptions
{
    /// <summary>
    /// Gets or sets the accepted timestamp drift for signed requests.
    /// </summary>
    public TimeSpan AllowedClockSkew { get; set; } = TimeSpan.FromMinutes(5);
}
