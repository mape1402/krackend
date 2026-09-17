namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Branching;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Represents the input required to resolve branch navigation.
/// </summary>
public sealed record OrchestrationBranchNavigationRequest
{
    /// <summary>
    /// Gets the orchestration instance.
    /// </summary>
    public required OrchestrationInstance Instance { get; init; }

    /// <summary>
    /// Gets all stages in the resolved artifact.
    /// </summary>
    public required IReadOnlyCollection<StageArtifact> Stages { get; init; }

    /// <summary>
    /// Gets the stage that owns the branch rules being evaluated.
    /// </summary>
    public required StageArtifact Stage { get; init; }

    /// <summary>
    /// Gets the source element type that completed.
    /// </summary>
    public required ElementType SourceType { get; init; }

    /// <summary>
    /// Gets the source element id that completed.
    /// </summary>
    public required Id SourceId { get; init; }

    /// <summary>
    /// Gets the source element key used for diagnostics.
    /// </summary>
    public required string SourceKey { get; init; }
}
