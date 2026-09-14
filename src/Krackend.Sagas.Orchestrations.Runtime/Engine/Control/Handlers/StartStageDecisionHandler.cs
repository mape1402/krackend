using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class StartStageDecisionHandler : IDecisionHandler<StartStageDecision>
    {
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IStageExecutionRepository _stageRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;

        public StartStageDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            IStageExecutionRepository stageRepository,
            IExecutionTransitionRepository transitionRepository)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        }

        public async Task HandleAsync(StartStageDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
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
    }
}
