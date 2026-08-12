using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Evaluates timeout policy configuration promoted by Design.
/// </summary>
public interface IRuntimeTimeoutPolicyEvaluator
{
    /// <summary>
    /// Evaluates timeout policy.
    /// </summary>
    /// <param name="timeoutPolicy">Timeout policy artifact fragment.</param>
    /// <returns>Runtime timeout policy.</returns>
    RuntimeTimeoutPolicy Evaluate(JsonObject timeoutPolicy);
}
