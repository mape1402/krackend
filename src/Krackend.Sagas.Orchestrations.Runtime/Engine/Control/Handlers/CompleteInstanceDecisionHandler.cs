using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class CompleteInstanceDecisionHandler : IDecisionHandler<CompleteInstanceDecision>
    {
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;

        public CompleteInstanceDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            IExecutionTransitionRepository transitionRepository)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        }

        public async Task HandleAsync(CompleteInstanceDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            instance.Status = OrchestrationInstanceStatus.Completed;
            instance.CompletedOnUtc = now;
            instance.LastUpdatedOnUtc = now;
            instance.FinalOutcome = "Completed";

            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                TransitionType = "InstanceCompleted",
                FromStatus = OrchestrationInstanceStatus.Running.ToString(),
                ToStatus = OrchestrationInstanceStatus.Completed.ToString(),
                OccurredOnUtc = now,
                Message = "Orchestration instance completed.",
                ProducedBy = nameof(CompleteInstanceDecisionHandler)
            }, cancellationToken);
        }
    }
}
