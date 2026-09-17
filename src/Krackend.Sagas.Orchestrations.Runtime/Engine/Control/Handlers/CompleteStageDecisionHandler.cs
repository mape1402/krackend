using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Branching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class CompleteStageDecisionHandler : IDecisionHandler<CompleteStageDecision>
    {
        private const string BranchNextStageIdMetadataKey = "Branch.NextStageId";
        private const string BranchNextStageKeyMetadataKey = "Branch.NextStageKey";
        private const string BranchRuleIdMetadataKey = "Branch.RuleId";

        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IStageExecutionRepository _stageRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IOrchestrationBranchNavigator _branchNavigator;

        public CompleteStageDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            IStageExecutionRepository stageRepository,
            IExecutionTransitionRepository transitionRepository,
            IOrchestrationBranchNavigator branchNavigator)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _branchNavigator = branchNavigator ?? throw new ArgumentNullException(nameof(branchNavigator));
        }

        public async Task HandleAsync(CompleteStageDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var stage = await _stageRepository.GetById(decision.StageExecutionId, cancellationToken);
            var branchNavigation = await _branchNavigator.ResolveAsync(
                new OrchestrationBranchNavigationRequest
                {
                    Instance = instance,
                    Stages = decision.Stages,
                    Stage = decision.Stage,
                    SourceType = ElementType.Stage,
                    SourceId = decision.Stage.Id,
                    SourceKey = decision.Stage.Key
                },
                cancellationToken);

            if (!branchNavigation.Succeeded)
            {
                await MarkBranchEvaluationFailedAsync(instance, stage, branchNavigation, now, cancellationToken);
                return;
            }

            stage.Status = StageExecutionStatus.Completed;
            stage.CompletedOnUtc = now;
            instance.CurrentStageKey = string.Empty;
            instance.CurrentTaskKey = string.Empty;
            instance.LastUpdatedOnUtc = now;

            if (branchNavigation.HasNavigation)
            {
                instance.Metadata[BranchNextStageIdMetadataKey] = JsonValue.Create(branchNavigation.TargetStage.Id.ToString());
                instance.Metadata[BranchNextStageKeyMetadataKey] = JsonValue.Create(branchNavigation.TargetStage.Key);
                instance.Metadata[BranchRuleIdMetadataKey] = JsonValue.Create(branchNavigation.BranchRuleId?.ToString() ?? string.Empty);
            }

            await _stageRepository.Update(stage, cancellationToken);
            if (branchNavigation.HasNavigation)
            {
                await SkipStagesBeforeBranchTargetAsync(
                    decision,
                    instance,
                    branchNavigation.TargetStage,
                    now,
                    cancellationToken);
            }

            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = stage.Id,
                TransitionType = "StageCompleted",
                FromStatus = StageExecutionStatus.Running.ToString(),
                ToStatus = StageExecutionStatus.Completed.ToString(),
                OccurredOnUtc = now,
                Message = $"Stage '{stage.StageKey}' completed.",
                ProducedBy = nameof(CompleteStageDecisionHandler)
            }, cancellationToken);

            if (branchNavigation.HasNavigation)
            {
                await _transitionRepository.Create(new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    StageExecutionId = stage.Id,
                    TransitionType = "BranchTaken",
                    FromStatus = StageExecutionStatus.Completed.ToString(),
                    ToStatus = branchNavigation.TargetStage.Key,
                    OccurredOnUtc = now,
                    Message = $"Branch rule '{branchNavigation.BranchRuleId}' navigated to stage '{branchNavigation.TargetStage.Key}'.",
                    ProducedBy = nameof(CompleteStageDecisionHandler)
                }, cancellationToken);
            }
        }

        private async Task MarkBranchEvaluationFailedAsync(
            OrchestrationInstance instance,
            StageExecution stage,
            OrchestrationBranchNavigationResult branchNavigation,
            DateTime now,
            CancellationToken cancellationToken)
        {
            stage.Status = StageExecutionStatus.Failed;
            stage.FailedOnUtc = now;
            stage.ErrorSummary = branchNavigation.ErrorMessage;
            stage.Metadata["BranchErrorCode"] = JsonValue.Create(branchNavigation.ErrorCode);
            stage.Metadata["BranchErrorMessage"] = JsonValue.Create(branchNavigation.ErrorMessage);
            CopyDiagnostics(stage.Metadata, branchNavigation.Diagnostics);

            instance.Status = OrchestrationInstanceStatus.Failed;
            instance.CurrentStageKey = stage.StageKey;
            instance.CurrentTaskKey = string.Empty;
            instance.FailedOnUtc = now;
            instance.ErrorSummary = branchNavigation.ErrorMessage;
            instance.LastUpdatedOnUtc = now;
            instance.WaitingSinceUtc = null;

            await _stageRepository.Update(stage, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = stage.Id,
                TransitionType = "BranchEvaluationFailed",
                FromStatus = StageExecutionStatus.Running.ToString(),
                ToStatus = StageExecutionStatus.Failed.ToString(),
                OccurredOnUtc = now,
                Message = branchNavigation.ErrorMessage,
                ProducedBy = nameof(CompleteStageDecisionHandler)
            }, cancellationToken);
        }

        private async Task SkipStagesBeforeBranchTargetAsync(
            CompleteStageDecision decision,
            OrchestrationInstance instance,
            Krackend.Sagas.Orchestrations.Abstractions.Artifacts.StageArtifact targetStage,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var existingStages = await _stageRepository.GetByInstanceId(instance.Id, cancellationToken);
            var stagesToSkip = decision.Stages
                .Where(stage => stage.Order > decision.Stage.Order && stage.Order < targetStage.Order)
                .Where(stage => existingStages.All(execution => !string.Equals(execution.StageKey, stage.Key, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(stage => stage.Order)
                .ToArray();

            foreach (var stageToSkip in stagesToSkip)
            {
                var skipped = new StageExecution
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    StageKey = stageToSkip.Key,
                    Order = stageToSkip.Order,
                    Status = StageExecutionStatus.Skipped,
                    WasSkipped = true,
                    SkipReason = $"Skipped by branch rule '{instance.Metadata[BranchRuleIdMetadataKey]?.GetValue<string>()}'.",
                    ExecutionConditionResult = null,
                    StartedOnUtc = now,
                    CompletedOnUtc = now,
                    ErrorSummary = string.Empty,
                    ParallelGroupCount = stageToSkip.ParallelGroups?.Count ?? 0
                };

                skipped.Metadata["BranchSkipped"] = JsonValue.Create(true);
                skipped.Metadata[BranchNextStageIdMetadataKey] = JsonValue.Create(targetStage.Id.ToString());
                skipped.Metadata[BranchNextStageKeyMetadataKey] = JsonValue.Create(targetStage.Key);

                await _stageRepository.Create(skipped, cancellationToken);
                await _transitionRepository.Create(new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    StageExecutionId = skipped.Id,
                    TransitionType = "StageSkipped",
                    FromStatus = StageExecutionStatus.Pending.ToString(),
                    ToStatus = StageExecutionStatus.Skipped.ToString(),
                    OccurredOnUtc = now,
                    Message = $"Stage '{stageToSkip.Key}' skipped by branch navigation to '{targetStage.Key}'.",
                    ProducedBy = nameof(CompleteStageDecisionHandler)
                }, cancellationToken);
            }
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
                metadata[$"Branch.{diagnostic.Key}"] = diagnostic.Value?.DeepClone();
            }
        }
    }
}
