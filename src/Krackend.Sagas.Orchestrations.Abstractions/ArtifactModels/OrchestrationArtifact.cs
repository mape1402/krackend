namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Defines orchestration artifact schema versions.
/// </summary>
public static class OrchestrationArtifactSchemaVersions
{
    /// <summary>
    /// Legacy/current linear stage artifact format without explicit extension metadata.
    /// </summary>
    public const int LinearV1 = 1;

    /// <summary>
    /// Extension-ready artifact format with explicit capabilities and execution policy metadata.
    /// </summary>
    public const int ExtensionReadyV2 = 2;

    /// <summary>
    /// Current artifact schema version.
    /// </summary>
    public const int Current = ExtensionReadyV2;
}

/// <summary>
/// Represents an immutable deployable orchestration artifact shared by Design and Runtime.
/// </summary>
public sealed record OrchestrationArtifact(
    Id OrchestrationDefinitionId,
    Id OrchestrationVersionId,
    string Key,
    string Name,
    string Domain,
    SemanticVersion Version,
    Checksum Checksum,
    IReadOnlyList<TriggerBindingArtifact> TriggerBindings,
    IReadOnlyList<VariableDefinitionArtifact> VariableDefinitions,
    IReadOnlyList<StageArtifact> StageDefinitions,
    string Description = "",
    string VersionLabel = "",
    string Notes = "")
{
    /// <summary>
    /// Gets the artifact schema version. Version 1 represents the current linear stage artifact format.
    /// </summary>
    public int ArtifactSchemaVersion { get; init; } = OrchestrationArtifactSchemaVersions.Current;

    /// <summary>
    /// Gets transversal metadata descriptor snapshots available to orchestration mappings and execution conditions.
    /// </summary>
    public IReadOnlyList<MetadataDescriptorArtifact> MetadataDescriptors { get; init; } = Array.Empty<MetadataDescriptorArtifact>();

    /// <summary>
    /// Gets orchestration-level execution policy defaults and constraints.
    /// </summary>
    public ExecutionPolicyArtifact ExecutionPolicy { get; init; } = ExecutionPolicyArtifact.Empty;

    /// <summary>
    /// Gets extension capabilities required by this orchestration artifact.
    /// </summary>
    public IReadOnlyList<RequiredCapabilityArtifact> RequiredCapabilities { get; init; } =
        Array.Empty<RequiredCapabilityArtifact>();

    /// <summary>
    /// Gets extension bundles required by this orchestration artifact.
    /// </summary>
    public IReadOnlyList<RequiredExtensionBundleArtifact> RequiredBundles { get; init; } =
        Array.Empty<RequiredExtensionBundleArtifact>();
}
