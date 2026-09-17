namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;

/// <summary>
/// Evaluates runtime orchestration execution conditions.
/// </summary>
public interface IOrchestrationConditionEvaluator
{
    /// <summary>
    /// Evaluates the configured condition for an orchestration element.
    /// </summary>
    /// <param name="request">Condition evaluation request.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The condition evaluation result.</returns>
    Task<OrchestrationConditionEvaluationResult> EvaluateAsync(
        OrchestrationConditionEvaluationRequest request,
        CancellationToken cancellationToken = default);
}
