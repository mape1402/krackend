namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

/// <summary>
/// Creates distribution artifact entities from immutable event payloads.
/// </summary>
public interface IArtifactFactory
{
    /// <summary>
    /// Creates a distribution artifact.
    /// </summary>
    /// <param name="orchestrationVersionId">Orchestration version identifier.</param>
    /// <param name="orchestrationDefinitionId">Orchestration definition identifier.</param>
    /// <param name="orchestrationDisplayName">Human-readable orchestration name.</param>
    /// <param name="versionLabel">Version label.</param>
    /// <param name="versionNumber">Semantic version number.</param>
    /// <param name="payload">Serialized artifact payload.</param>
    /// <param name="checksum">Artifact checksum.</param>
    /// <param name="sourceEvent">Event that produced the artifact.</param>
    /// <param name="occurredAtUtc">Event occurrence timestamp.</param>
    /// <returns>The created artifact entity.</returns>
    Artifact Create(
        string orchestrationVersionId,
        string orchestrationDefinitionId,
        string orchestrationDisplayName,
        string versionLabel,
        string versionNumber,
        string payload,
        string checksum,
        string sourceEvent,
        DateTime occurredAtUtc);
}
