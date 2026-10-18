namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Records one control-plane artifact application in a runtime node.
/// </summary>
public sealed class RuntimeArtifactDeploymentRecord
{
    /// <summary>Gets or sets the immutable runtime history id.</summary>
    public Id Id { get; set; }

    /// <summary>Gets or sets the runtime artifact affected by this application.</summary>
    public Id RuntimeOrchestrationArtifactId { get; set; }

    /// <summary>Gets or sets the orchestration key for grouping history.</summary>
    public required string OrchestrationDefinitionKey { get; set; }

    /// <summary>Gets or sets the orchestration version for grouping history.</summary>
    public required string OrchestrationVersion { get; set; }

    /// <summary>Gets or sets the control-plane deployed artifact id.</summary>
    public required string ControlPlaneArtifactId { get; set; }

    /// <summary>Gets or sets the release that applied this artifact.</summary>
    public string ReleaseId { get; set; } = string.Empty;

    /// <summary>Gets or sets the typed deployed artifact operation.</summary>
    public required string ArtifactType { get; set; }

    /// <summary>Gets or sets the direct or rollout strategy.</summary>
    public required string ReleaseStrategy { get; set; }

    /// <summary>Gets or sets the effective artifact lifecycle after application.</summary>
    public RuntimeOrchestrationLifecycleStatus LifecycleStatus { get; set; }

    /// <summary>Gets or sets the effective runtime installation status after application.</summary>
    public RuntimeOrchestrationArtifactStatus RuntimeStatus { get; set; }

    /// <summary>Gets or sets when this runtime applied the artifact.</summary>
    public DateTime AppliedOnUtc { get; set; }
}
