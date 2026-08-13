using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Evaluates runtime branch rules promoted by Design.
/// </summary>
internal interface IRuntimeBranchRuleEvaluator
{
    /// <summary>
    /// Evaluates branch rules after a stage has completed.
    /// </summary>
    /// <param name="document">Runtime artifact document.</param>
    /// <param name="stage">Completed stage.</param>
    /// <param name="payload">Current runtime payload.</param>
    /// <returns>Branch decision.</returns>
    RuntimeBranchDecision EvaluateStage(RuntimeArtifactDocument document, RuntimeStageDocument stage, JsonNode payload);
}
