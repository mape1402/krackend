using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents runtime orchestration artifact in the orchestrator domain.
/// </summary>
public sealed class RuntimeOrchestrationArtifact
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets environment key.
    /// </summary>
    public required string EnvironmentKey { get; set; }

    /// <summary>
    /// Gets or sets orchestration definition key.
    /// </summary>
    public required string OrchestrationDefinitionKey { get; set; }

    /// <summary>
    /// Gets or sets artifact type.
    /// </summary>
    public required string ArtifactType { get; set; }

    /// <summary>
    /// Gets or sets source orchestration version id.
    /// </summary>
    public Id SourceOrchestrationVersionId { get; set; }

    /// <summary>
    /// Gets or sets version.
    /// </summary>
    public required SemanticVersion Version { get; set; }

    /// <summary>
    /// Gets or sets artifact checksum.
    /// </summary>
    public required Checksum ArtifactChecksum { get; set; }

    /// <summary>
    /// Gets or sets artifact payload.
    /// </summary>
    public required JsonNode ArtifactPayload { get; set; }

    /// <summary>
    /// Gets or sets the artifact installation status.
    /// </summary>
    public RuntimeOrchestrationArtifactStatus Status { get; set; } = RuntimeOrchestrationArtifactStatus.Pending;

    /// <summary>
    /// Gets or sets the ingress configuration generation represented by this artifact.
    /// </summary>
    public long IngressGeneration { get; set; }

    /// <summary>
    /// Gets or sets is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets loaded to cache.
    /// </summary>
    public bool LoadedToCache { get; set; }

    /// <summary>
    /// Gets or sets deployed on utc.
    /// </summary>
    public DateTime DeployedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets activated on utc.
    /// </summary>
    public DateTime? ActivatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets when ingress configuration projection started.
    /// </summary>
    public DateTime? ProjectionStartedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets when ingress configuration projection completed.
    /// </summary>
    public DateTime? ProjectionCompletedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets when ingress configuration projection failed.
    /// </summary>
    public DateTime? ProjectionFailedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the last ingress configuration projection error.
    /// </summary>
    public string ProjectionError { get; set; }

    /// <summary>
    /// Gets or sets retired on utc.
    /// </summary>
    public DateTime? RetiredOnUtc { get; set; }

    /// <summary>
    /// Gets or sets superseded by artifact id.
    /// </summary>
    public Id? SupersededByArtifactId { get; set; }

    /// <summary>
    /// Gets or sets notes.
    /// </summary>
    public string Notes { get; set; }
}
