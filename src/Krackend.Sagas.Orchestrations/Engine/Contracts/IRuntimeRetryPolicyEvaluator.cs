using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Evaluates retry policy configuration promoted by Design.
/// </summary>
public interface IRuntimeRetryPolicyEvaluator
{
    /// <summary>
    /// Evaluates retry policy.
    /// </summary>
    /// <param name="retryPolicy">Retry policy artifact fragment.</param>
    /// <returns>Runtime retry policy.</returns>
    RuntimeRetryPolicy Evaluate(JsonObject retryPolicy);
}
