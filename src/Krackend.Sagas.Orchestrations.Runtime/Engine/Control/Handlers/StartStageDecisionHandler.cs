using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class StartStageDecisionHandler : IDecisionHandler<StartStageDecision>
    {
        private const string BranchNextStageIdMetadataKey = "Branch.NextStageId";
        private const string BranchNextStageKeyMetadataKey = "Branch.NextStageKey";
        private const string BranchRuleIdMetadataKey = "Branch.RuleId";

        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IStageExecutionRepository _stageRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IOrchestrationConditionEvaluator _conditionEvaluator;
        private readonly IOrchestrationPayloadContextFactory _payloadContextFactory;

        public StartStageDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            IStageExecutionRepository stageRepository,
            IExecutionTransitionRepository transitionRepository,
            IOrchestrationConditionEvaluator conditionEvaluator,
            IOrchestrationPayloadContextFactory payloadContextFactory)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
            _payloadContextFactory = payloadContextFactory ?? throw new ArgumentNullException(nameof(payloadContextFactory));
        }

        public async Task HandleAsync(StartStageDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var condition = await _conditionEvaluator.EvaluateAsync(
                new OrchestrationConditionEvaluationRequest
                {
                    Condition = decision.Stage.ExecutionCondition,
                    PayloadContext = _payloadContextFactory.Create(instance, decision.Stage.Key, string.Empty),
                    ElementKey = decision.Stage.Key,
                    Phase = "Stage"
                },
                cancellationToken);

            if (!condition.Succeeded)
            {
                await MarkConditionFailedAsync(decision, instance, condition, now, cancellationToken);
                return;
            }

            if (!condition.ShouldExecute)
            {
                await SkipStageAsync(decision, instance, now, cancellationToken);
                return;
            }

            var stageExecution = new StageExecution
            {
                Id = Id.New(),
                OrchestrationInstanceId = decision.InstanceId,
                StageKey = decision.Stage.Key,
                Order = decision.Stage.Order,
                Status = StageExecutionStatus.Running,
                WasSkipped = false,
                SkipReason = string.Empty,
                ExecutionConditionResult = true,
                StartedOnUtc = now,
                ErrorSummary = string.Empty,
                ParallelGroupCount = decision.Stage.ParallelGroups?.Count ?? 0
            };

            instance.Status = OrchestrationInstanceStatus.Running;
            instance.CurrentStageKey = decision.Stage.Key;
            instance.CurrentTaskKey = string.Empty;
            instance.LastUpdatedOnUtc = now;
            instance.WaitingSinceUtc = null;
            ClearBranchNavigation(instance, decision.Stage.Id.ToString());

            await _stageRepository.Create(stageExecution, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = stageExecution.Id,
                TransitionType = "StageStarted",
                FromStatus = StageExecutionStatus.Pending.ToString(),
                ToStatus = StageExecutionStatus.Running.ToString(),
                OccurredOnUtc = now,
                Message = $"Stage '{decision.Stage.Key}' started.",
                ProducedBy = nameof(StartStageDecisionHandler)
            }, cancellationToken);
        }

        private async Task MarkConditionFailedAsync(
            StartStageDecision decision,
            OrchestrationInstance instance,
            OrchestrationConditionEvaluationResult condition,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var stageExecution = new StageExecution
            {
                Id = Id.New(),
                OrchestrationInstanceId = decision.InstanceId,
                StageKey = decision.Stage.Key,
                Order = decision.Stage.Order,
                Status = StageExecutionStatus.Failed,
                WasSkipped = false,
                SkipReason = string.Empty,
                ExecutionConditionResult = null,
                StartedOnUtc = now,
                FailedOnUtc = now,
                ErrorSummary = condition.ErrorMessage,
                ParallelGroupCount = decision.Stage.ParallelGroups?.Count ?? 0
            };

            stageExecution.Metadata["ConditionErrorCode"] = JsonValue.Create(condition.ErrorCode);
            stageExecution.Metadata["ConditionErrorMessage"] = JsonValue.Create(condition.ErrorMessage);
            CopyDiagnostics(stageExecution.Metadata, condition.Diagnostics);

            instance.Status = OrchestrationInstanceStatus.Failed;
            instance.CurrentStageKey = decision.Stage.Key;
            instance.CurrentTaskKey = string.Empty;
            instance.FailedOnUtc = now;
            instance.ErrorSummary = condition.ErrorMessage;
            instance.LastUpdatedOnUtc = now;
            instance.WaitingSinceUtc = null;
            ClearBranchNavigation(instance, decision.Stage.Id.ToString());

            await _stageRepository.Create(stageExecution, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = stageExecution.Id,
                TransitionType = "StageConditionFailed",
                FromStatus = StageExecutionStatus.Pending.ToString(),
                ToStatus = StageExecutionStatus.Failed.ToString(),
                OccurredOnUtc = now,
                Message = condition.ErrorMessage,
                ProducedBy = nameof(StartStageDecisionHandler)
            }, cancellationToken);
        }

        private async Task SkipStageAsync(
            StartStageDecision decision,
            OrchestrationInstance instance,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var stageExecution = new StageExecution
            {
                Id = Id.New(),
                OrchestrationInstanceId = decision.InstanceId,
                StageKey = decision.Stage.Key,
                Order = decision.Stage.Order,
                Status = StageExecutionStatus.Skipped,
                WasSkipped = true,
                SkipReason = "Execution condition evaluated to false.",
                ExecutionConditionResult = false,
                StartedOnUtc = now,
                CompletedOnUtc = now,
                ErrorSummary = string.Empty,
                ParallelGroupCount = decision.Stage.ParallelGroups?.Count ?? 0
            };

            instance.Status = OrchestrationInstanceStatus.Running;
            instance.CurrentStageKey = string.Empty;
            instance.CurrentTaskKey = string.Empty;
            instance.LastUpdatedOnUtc = now;
            instance.WaitingSinceUtc = null;
            ClearBranchNavigation(instance, decision.Stage.Id.ToString());

            await _stageRepository.Create(stageExecution, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = stageExecution.Id,
                TransitionType = "StageSkipped",
                FromStatus = StageExecutionStatus.Pending.ToString(),
                ToStatus = StageExecutionStatus.Skipped.ToString(),
                OccurredOnUtc = now,
                Message = $"Stage '{decision.Stage.Key}' skipped because its execution condition evaluated to false.",
                ProducedBy = nameof(StartStageDecisionHandler)
            }, cancellationToken);
        }

        private static void CopyDiagnostics(
            IDictionary<string, JsonNode> metadata,
            IReadOnlyDictionary<string, JsonNode> diagnostics)
        {
            if (diagnostics is null)
            {
                return;
            }

            foreach (var diagnostic in diagnostics)
            {
                metadata[$"Condition.{diagnostic.Key}"] = diagnostic.Value?.DeepClone();
            }
        }

        private static void ClearBranchNavigation(OrchestrationInstance instance, string stageId)
        {
            var branchTarget = TryGetString(instance.Metadata, BranchNextStageIdMetadataKey);
            if (!string.Equals(branchTarget, stageId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            instance.Metadata.Remove(BranchNextStageIdMetadataKey);
            instance.Metadata.Remove(BranchNextStageKeyMetadataKey);
            instance.Metadata.Remove(BranchRuleIdMetadataKey);
        }

        private static string TryGetString(
            IReadOnlyDictionary<string, JsonNode> metadata,
            string key)
        {
            if (metadata is null ||
                !metadata.TryGetValue(key, out var value) ||
                value is null)
            {
                return null;
            }

            return value.GetValueKind() == System.Text.Json.JsonValueKind.String
                ? value.GetValue<string>()
                : value.ToJsonString();
        }
    }
}
