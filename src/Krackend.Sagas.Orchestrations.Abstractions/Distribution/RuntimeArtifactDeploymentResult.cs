namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution;

/// <summary>
/// Describes the result of installing an artifact package into a runtime node.
/// </summary>
public sealed class RuntimeArtifactDeploymentResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the runtime accepted the artifact.
    /// </summary>
    public bool Accepted { get; set; }

    /// <summary>
    /// Gets or sets the runtime artifact identifier.
    /// </summary>
    public string RuntimeArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the deployment status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the deployment message.
    /// </summary>
    public string Message { get; set; }
}
