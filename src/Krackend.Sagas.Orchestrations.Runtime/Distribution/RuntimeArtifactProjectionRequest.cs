namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Represents a request to project runtime ingress configuration from an artifact.
/// </summary>
public sealed class RuntimeArtifactProjectionRequest
{
    /// <summary>
    /// Gets or sets the runtime artifact id.
    /// </summary>
    public required string ArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the ingress generation to project.
    /// </summary>
    public long IngressGeneration { get; set; }

    /// <summary>
    /// Gets or sets the component that requested the projection.
    /// </summary>
    public string RequestedBy { get; set; }

    /// <summary>
    /// Gets or sets when the projection was requested.
    /// </summary>
    public DateTime RequestedOnUtc { get; set; }
}
