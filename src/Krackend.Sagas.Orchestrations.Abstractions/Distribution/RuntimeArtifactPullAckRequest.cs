namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution;

/// <summary>
/// Acknowledges a pulled artifact after a runtime node installs it.
/// </summary>
public sealed class RuntimeArtifactPullAckRequest
{
    /// <summary>
    /// Gets or sets the runtime artifact identifier created by the receiving runtime node.
    /// </summary>
    public string RuntimeArtifactId { get; set; }
}
