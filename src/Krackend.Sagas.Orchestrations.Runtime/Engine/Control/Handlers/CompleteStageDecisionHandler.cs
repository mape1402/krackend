using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class CompleteStageDecisionHandler : IDecisionHandler<CompleteStageDecision>
    {
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IStageExecutionRepository _stageRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;

        public CompleteStageDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            IStageExecutionRepository stageRepository,
            IExecutionTransitionRepository transitionRepository)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        }

        public async Task HandleAsync(CompleteStageDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var stage = await _stageRepository.GetById(decision.StageExecutionId, cancellationToken);

            stage.Status = StageExecutionStatus.Completed;
            stage.CompletedOnUtc = now;
            instance.CurrentStageKey = string.Empty;
            instance.CurrentTaskKey = string.Empty;
            instance.LastUpdatedOnUtc = now;

            await _stageRepository.Update(stage, cancellationToken);
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
        }
    }
}
