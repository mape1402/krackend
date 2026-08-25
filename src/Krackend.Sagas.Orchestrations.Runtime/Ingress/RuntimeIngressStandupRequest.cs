namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Represents a local ingress standup request.
/// </summary>
public sealed class RuntimeIngressStandupRequest
{
    /// <summary>
    /// Gets or sets the runtime artifact id.
    /// </summary>
    public required string ArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the ingress generation to apply.
    /// </summary>
    public long IngressGeneration { get; set; }

    /// <summary>
    /// Gets or sets why the standup was requested.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets when the standup was requested.
    /// </summary>
    public DateTime RequestedOnUtc { get; set; }
}
