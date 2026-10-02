namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

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
    public int ArtifactSchemaVersion { get; init; } = 1;

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
