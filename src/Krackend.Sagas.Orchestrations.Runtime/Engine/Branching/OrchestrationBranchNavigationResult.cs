namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Branching;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the result of resolving runtime branch navigation.
/// </summary>
public sealed record OrchestrationBranchNavigationResult
{
    /// <summary>
    /// Gets whether branch evaluation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets whether a branch rule matched and selected a target stage.
    /// </summary>
    public bool HasNavigation { get; init; }

    /// <summary>
    /// Gets the branch rule id that was selected.
    /// </summary>
    public Id? BranchRuleId { get; init; }

    /// <summary>
    /// Gets the selected target stage.
    /// </summary>
    public StageArtifact TargetStage { get; init; }

    /// <summary>
    /// Gets the failure code when branch evaluation fails.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the failure message when branch evaluation fails.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets diagnostic metadata emitted while resolving branch navigation.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode> Diagnostics { get; init; } =
        new Dictionary<string, JsonNode>(StringComparer.Ordinal);

    /// <summary>
    /// Creates a result where no branch matched.
    /// </summary>
    /// <returns>A branch navigation result without navigation.</returns>
    public static OrchestrationBranchNavigationResult None()
        => new()
        {
            Succeeded = true,
            HasNavigation = false
        };

    /// <summary>
    /// Creates a result where a branch selected a target stage.
    /// </summary>
    /// <param name="ruleId">Selected branch rule id.</param>
    /// <param name="targetStage">Selected target stage.</param>
    /// <returns>A branch navigation result with navigation.</returns>
    public static OrchestrationBranchNavigationResult Navigate(Id ruleId, StageArtifact targetStage)
        => new()
        {
            Succeeded = true,
            HasNavigation = true,
            BranchRuleId = ruleId,
            TargetStage = targetStage
        };

    /// <summary>
    /// Creates a failed branch navigation result.
    /// </summary>
    /// <param name="errorCode">Failure code.</param>
    /// <param name="errorMessage">Failure message.</param>
    /// <param name="diagnostics">Optional diagnostic metadata.</param>
    /// <returns>A failed branch navigation result.</returns>
    public static OrchestrationBranchNavigationResult Failure(
        string errorCode,
        string errorMessage,
        IReadOnlyDictionary<string, JsonNode> diagnostics = null)
        => new()
        {
            Succeeded = false,
            HasNavigation = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Diagnostics = diagnostics ?? new Dictionary<string, JsonNode>(StringComparer.Ordinal)
        };
}
