namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution;

/// <summary>
/// Contains the artifact snapshot and routing information delivered from a control plane to a runtime node.
/// </summary>
public sealed class RuntimeArtifactDeliveryPackage
{
    /// <summary>
    /// Gets or sets the release target identifier assigned by the source control plane.
    /// </summary>
    public string ReleaseTargetId { get; set; }

    /// <summary>
    /// Gets or sets the artifact identifier assigned by the source control plane.
    /// </summary>
    public string ArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the artifact type.
    /// </summary>
    public string ArtifactType { get; set; }

    /// <summary>
    /// Gets or sets the artifact schema version.
    /// </summary>
    public string SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the target runtime environment key.
    /// </summary>
    public string EnvironmentKey { get; set; }

    /// <summary>
    /// Gets or sets the orchestration definition identifier from the source control plane.
    /// </summary>
    public string OrchestrationDefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration version identifier from the source control plane.
    /// </summary>
    public string OrchestrationVersionId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration definition key used by runtime ingress and dispatch logic.
    /// </summary>
    public string OrchestrationDefinitionKey { get; set; }

    /// <summary>
    /// Gets or sets the orchestration version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the artifact checksum.
    /// </summary>
    public string Checksum { get; set; }

    /// <summary>
    /// Gets or sets the serialized orchestration artifact payload.
    /// </summary>
    public string PayloadJson { get; set; }

    /// <summary>
    /// Gets or sets the promotion correlation identifier.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the actor that promoted the artifact.
    /// </summary>
    public string PromotedBy { get; set; }

    /// <summary>
    /// Gets or sets when the artifact was promoted.
    /// </summary>
    public DateTime PromotedOnUtc { get; set; }
}
