using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class CompleteInstanceDecisionHandler : IDecisionHandler<CompleteInstanceDecision>
    {
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IOrchestrationDispatchCancellationCoordinator _dispatchCancellationCoordinator;
        private readonly IRuntimeStateTransaction _stateTransaction;

        public CompleteInstanceDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            IExecutionTransitionRepository transitionRepository,
            IOrchestrationDispatchCancellationCoordinator dispatchCancellationCoordinator,
            IRuntimeStateTransaction stateTransaction = null)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _dispatchCancellationCoordinator = dispatchCancellationCoordinator ?? throw new ArgumentNullException(nameof(dispatchCancellationCoordinator));
            _stateTransaction = stateTransaction;
        }

        public async Task HandleAsync(CompleteInstanceDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            if (IsTerminal(instance.Status))
            {
                return;
            }

            var previousStatus = instance.Status;
            instance.Status = OrchestrationInstanceStatus.Completed;
            instance.CompletedOnUtc = now;
            instance.LastUpdatedOnUtc = now;
            instance.FinalOutcome = "Completed";
            instance.DispatchCancellationPending = true;

            await PersistStateAsync(async token =>
            {
                await _instanceRepository.Update(instance, token);
                await _transitionRepository.Create(new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    TransitionType = "InstanceCompleted",
                    FromStatus = previousStatus.ToString(),
                    ToStatus = OrchestrationInstanceStatus.Completed.ToString(),
                    OccurredOnUtc = now,
                    Message = "Orchestration instance completed.",
                    ProducedBy = nameof(CompleteInstanceDecisionHandler)
                }, token);
            }, cancellationToken);
            await _dispatchCancellationCoordinator.CancelPendingAsync(
                instance,
                "Orchestration instance completed before this durable dispatch was published.",
                cancellationToken);
        }

        private static bool IsTerminal(OrchestrationInstanceStatus status)
            => status is OrchestrationInstanceStatus.Completed
                or OrchestrationInstanceStatus.CompletedWithErrors
                or OrchestrationInstanceStatus.Failed
                or OrchestrationInstanceStatus.DeadLettered
                or OrchestrationInstanceStatus.Compensating
                or OrchestrationInstanceStatus.Compensated
                or OrchestrationInstanceStatus.Aborted
                or OrchestrationInstanceStatus.Stopped;

        private async Task PersistStateAsync(Func<CancellationToken, Task> mutation, CancellationToken cancellationToken)
        {
            if (_stateTransaction is null)
            {
                await mutation(cancellationToken);
                return;
            }

            await _stateTransaction.ExecuteAsync(mutation, cancellationToken);
        }
    }
}
