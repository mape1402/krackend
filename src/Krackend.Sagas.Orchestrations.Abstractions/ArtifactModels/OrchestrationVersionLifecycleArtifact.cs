namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

/// <summary>
/// Defines the compact payload used to apply an orchestration version lifecycle action at runtime.
/// </summary>
/// <remarks>
/// Lifecycle artifacts intentionally do not contain the executable orchestration graph. The runtime
/// retains the previously deployed new-version artifact and uses this envelope only to verify and
/// apply a deprecation or archive action against that exact snapshot.
/// </remarks>
public sealed class OrchestrationVersionLifecycleArtifact
{
    /// <summary>
    /// Identifies this payload as a lifecycle envelope rather than an executable orchestration snapshot.
    /// </summary>
    public const string Kind = "orchestration-version-lifecycle";

    /// <summary>
    /// Gets or sets the lifecycle payload schema version.
    /// </summary>
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>
    /// Gets or sets the stable lifecycle payload discriminator.
    /// </summary>
    public string ArtifactKind { get; init; } = Kind;

    /// <summary>
    /// Gets or sets the lifecycle operation to apply: <c>deprecate</c> or <c>archive</c>.
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the control-plane orchestration definition identifier.
    /// </summary>
    public string OrchestrationDefinitionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the orchestration definition key used by runtime lookup and ingress.
    /// </summary>
    public string OrchestrationDefinitionKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the control-plane orchestration version identifier.
    /// </summary>
    public string OrchestrationVersionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the semantic orchestration version.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the checksum of the executable new-version artifact the action targets.
    /// </summary>
    public string TargetArtifactChecksum { get; init; } = string.Empty;
}
