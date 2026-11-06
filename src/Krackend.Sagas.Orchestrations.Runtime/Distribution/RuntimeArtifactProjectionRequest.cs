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

    /// <summary>Trigger identities reserved for the rollout, used to reject stale handoffs.</summary>
    public string[] RolloutTriggerIdentities { get; set; } = [];

    /// <summary>Previous trigger configurations to deactivate when projection succeeds.</summary>
    public RuntimeArtifactTriggerReplacement[] RolloutReplacements { get; set; } = [];

    /// <summary>Existing target trigger keys to reactivate for a manual rollback.</summary>
    public string[] RollbackTargetTriggerKeys { get; set; } = [];

    /// <summary>Stable identifier used to refresh replicas after a rollback commits.</summary>
    public string RollbackOperationId { get; set; }
}

/// <summary>Identifies the previous ingress configurations affected by a rollout.</summary>
public sealed class RuntimeArtifactTriggerReplacement
{
    /// <summary>Previous runtime artifact identifier.</summary>
    public required string ArtifactId { get; set; }

    /// <summary>Trigger configuration keys to replace.</summary>
    public string[] ConfigurationKeys { get; set; } = [];

    /// <summary>Source-only identities whose reservations must be released after a rollback handoff.</summary>
    public string[] ReleaseTriggerIdentities { get; set; } = [];
}
