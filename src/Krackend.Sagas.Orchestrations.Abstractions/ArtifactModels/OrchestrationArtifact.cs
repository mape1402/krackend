namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

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
    string Notes = "");
