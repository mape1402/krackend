namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Branching;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

/// <summary>
/// Resolves branch rules using the configured condition evaluator.
/// </summary>
public sealed class DefaultOrchestrationBranchNavigator : IOrchestrationBranchNavigator
{
    private readonly IOrchestrationConditionEvaluator _conditionEvaluator;
    private readonly IOrchestrationPayloadContextFactory _payloadContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOrchestrationBranchNavigator"/> class.
    /// </summary>
    /// <param name="conditionEvaluator">Condition evaluator.</param>
    /// <param name="payloadContextFactory">Payload context factory.</param>
    public DefaultOrchestrationBranchNavigator(
        IOrchestrationConditionEvaluator conditionEvaluator,
        IOrchestrationPayloadContextFactory payloadContextFactory)
    {
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        _payloadContextFactory = payloadContextFactory ?? throw new ArgumentNullException(nameof(payloadContextFactory));
    }

    /// <inheritdoc />
    public async Task<OrchestrationBranchNavigationResult> ResolveAsync(
        OrchestrationBranchNavigationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var branchRules = request.Stage.BranchRules?
            .Where(rule => rule.FromType == request.SourceType && rule.FromId == request.SourceId)
            .ToArray();
        if (branchRules is null || branchRules.Length == 0)
        {
            return OrchestrationBranchNavigationResult.None();
        }

        foreach (var rule in branchRules)
        {
            if (rule.NavigateToType != ElementType.Stage)
            {
                return OrchestrationBranchNavigationResult.Failure(
                    "BranchTargetTypeNotSupported",
                    $"Branch rule '{rule.Id}' navigates to '{rule.NavigateToType}', but runtime branch navigation currently supports stage targets.");
            }

            var targetStage = request.Stages.FirstOrDefault(stage => stage.Id == rule.NavigateToId);
            if (targetStage is null)
            {
                return OrchestrationBranchNavigationResult.Failure(
                    "BranchTargetNotFound",
                    $"Branch rule '{rule.Id}' targets stage '{rule.NavigateToId}', but that stage does not exist in the artifact.");
            }

            if (targetStage.Order <= request.Stage.Order)
            {
                return OrchestrationBranchNavigationResult.Failure(
                    "BranchTargetOrderNotSupported",
                    $"Branch rule '{rule.Id}' targets stage '{targetStage.Key}' with order '{targetStage.Order}', but runtime branch navigation only supports forward stage targets.");
            }

            var condition = await _conditionEvaluator.EvaluateAsync(
                new OrchestrationConditionEvaluationRequest
                {
                    Condition = rule.Condition,
                    PayloadContext = _payloadContextFactory.Create(request.Instance, request.Stage.Key, request.SourceKey),
                    ElementKey = rule.Id.ToString(),
                    Phase = "Branch"
                },
                cancellationToken);
            if (!condition.Succeeded)
            {
                return OrchestrationBranchNavigationResult.Failure(
                    string.IsNullOrWhiteSpace(condition.ErrorCode) ? "BranchConditionFailed" : condition.ErrorCode,
                    string.IsNullOrWhiteSpace(condition.ErrorMessage) ? "Branch condition evaluation failed." : condition.ErrorMessage,
                    condition.Diagnostics);
            }

            if (condition.ShouldExecute)
            {
                return OrchestrationBranchNavigationResult.Navigate(rule.Id, targetStage);
            }
        }

        return OrchestrationBranchNavigationResult.None();
    }
}
