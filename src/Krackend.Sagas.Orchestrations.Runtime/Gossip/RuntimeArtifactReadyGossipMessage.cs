using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip;

/// <summary>
/// Represents an artifact-ready notification exchanged between runtime replicas.
/// </summary>
public sealed class RuntimeArtifactReadyGossipMessage
{
    /// <summary>
    /// Gets or sets the runtime artifact id.
    /// </summary>
    public required string ArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration definition key.
    /// </summary>
    public required string OrchestrationDefinitionKey { get; set; }

    /// <summary>
    /// Gets or sets the orchestration version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the ingress generation that became ready.
    /// </summary>
    public long IngressGeneration { get; set; }

    /// <summary>
    /// Gets or sets when the notification was created.
    /// </summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>
    /// Creates a gossip message from a runtime artifact.
    /// </summary>
    /// <param name="artifact">Runtime artifact.</param>
    /// <returns>Artifact-ready gossip message.</returns>
    public static RuntimeArtifactReadyGossipMessage FromArtifact(RuntimeOrchestrationArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        return new RuntimeArtifactReadyGossipMessage
        {
            ArtifactId = artifact.Id.ToString(),
            OrchestrationDefinitionKey = artifact.OrchestrationDefinitionKey,
            Version = artifact.Version.ToString(),
            IngressGeneration = artifact.IngressGeneration,
            OccurredOnUtc = DateTime.UtcNow
        };
    }
}
