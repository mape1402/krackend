namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution;

/// <summary>
/// Describes the result of a control-plane artifact delivery operation.
/// </summary>
public sealed class RuntimeArtifactDeliveryResult
{
    /// <summary>
    /// Gets or sets a value indicating whether delivery succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the release target identifier.
    /// </summary>
    public string ReleaseTargetId { get; set; }

    /// <summary>
    /// Gets or sets the runtime node identifier as known by the source control plane.
    /// </summary>
    public string RuntimeNodeId { get; set; }

    /// <summary>
    /// Gets or sets the delivered artifact identifier.
    /// </summary>
    public string ArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the delivery status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets a delivery status message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the external runtime artifact reference.
    /// </summary>
    public string ExternalReference { get; set; }
}
