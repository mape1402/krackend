namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Owns one normalized trigger identity for a runtime artifact.
/// </summary>
/// <remarks>
/// The identity is globally unique in the runtime store. It provides a durable
/// concurrency boundary when more than one runtime host processes releases.
/// </remarks>
public sealed class RuntimeTriggerClaim
{
    /// <summary>
    /// Gets or sets the normalized transport/topic/version identity.
    /// </summary>
    public required string Identity { get; set; }

    /// <summary>
    /// Gets or sets the artifact that currently owns the trigger.
    /// </summary>
    public Id RuntimeOrchestrationArtifactId { get; set; }

    /// <summary>
    /// Gets or sets when ownership was last confirmed or transferred.
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }
}
