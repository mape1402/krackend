namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

/// <summary>
/// Provides safe default condition evaluation when no condition adapter is configured.
/// </summary>
public sealed class DefaultOrchestrationConditionEvaluator : IOrchestrationConditionEvaluator
{
    /// <inheritdoc />
    public Task<OrchestrationConditionEvaluationResult> EvaluateAsync(
        OrchestrationConditionEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.Condition?.IsEnabled != true)
        {
            return Task.FromResult(OrchestrationConditionEvaluationResult.Success(true));
        }

        if (request.Condition.Configuration is not DslConditionConfigurationArtifact dsl)
        {
            return Task.FromResult(OrchestrationConditionEvaluationResult.Failure(
                "ConditionConfigurationNotSupported",
                $"Condition configuration '{request.Condition.Configuration?.GetType().Name ?? "Unknown"}' is not supported."));
        }

        var expression = dsl.Expression.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(OrchestrationConditionEvaluationResult.Failure(
                "ConditionExpressionMissing",
                $"The {request.Phase} condition for '{request.ElementKey}' is enabled but does not contain an expression."));
        }

        if (bool.TryParse(expression, out var literalResult))
        {
            return Task.FromResult(OrchestrationConditionEvaluationResult.Success(literalResult));
        }

        return Task.FromResult(OrchestrationConditionEvaluationResult.Failure(
            "ConditionAdapterNotConfigured",
            $"The {request.Phase} condition for '{request.ElementKey}' requires a DSL condition adapter."));
    }
}
