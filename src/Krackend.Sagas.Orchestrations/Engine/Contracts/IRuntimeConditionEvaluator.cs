using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Evaluates runtime execution conditions promoted by Design.
/// </summary>
public interface IRuntimeConditionEvaluator
{
    /// <summary>
    /// Evaluates the supplied condition against the current payload.
    /// </summary>
    /// <param name="condition">Condition artifact fragment.</param>
    /// <param name="payload">Current runtime payload.</param>
    /// <returns>Condition evaluation result.</returns>
    RuntimeConditionEvaluation Evaluate(JsonObject condition, JsonNode payload);
}
