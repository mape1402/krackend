using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class DeadLetterInstanceDecisionHandler : IDecisionHandler<DeadLetterInstanceDecision>
    {
        private const string DeadLetteredOnUtcMetadataKey = "DeadLetteredOnUtc";
        private const string DeadLetterReasonMetadataKey = "DeadLetterReason";
        private const string RecoveryAllowedMetadataKey = "Recovery.Allowed";

        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;

        public DeadLetterInstanceDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            IExecutionTransitionRepository transitionRepository)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        }

        public async Task HandleAsync(DeadLetterInstanceDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            if (IsHardTerminal(instance.Status) || instance.Status == OrchestrationInstanceStatus.DeadLettered)
            {
                return;
            }

            var previousStatus = instance.Status;
            var reason = string.IsNullOrWhiteSpace(decision.Reason)
                ? "Orchestration instance entered dead-letter state after an unrecovered failure."
                : decision.Reason;

            instance.Status = OrchestrationInstanceStatus.DeadLettered;
            instance.FailedOnUtc ??= now;
            instance.WaitingSinceUtc = null;
            instance.ErrorSummary = string.IsNullOrWhiteSpace(instance.ErrorSummary) ? reason : instance.ErrorSummary;
            instance.LastUpdatedOnUtc = now;
            instance.Metadata[DeadLetteredOnUtcMetadataKey] = JsonValue.Create(now);
            instance.Metadata[DeadLetterReasonMetadataKey] = JsonValue.Create(reason);
            instance.Metadata[RecoveryAllowedMetadataKey] = JsonValue.Create(true);

            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = decision.StageExecutionId,
                TaskExecutionId = decision.TaskExecutionId,
                TransitionType = "InstanceDeadLettered",
                FromStatus = previousStatus.ToString(),
                ToStatus = OrchestrationInstanceStatus.DeadLettered.ToString(),
                OccurredOnUtc = now,
                Message = reason,
                ProducedBy = nameof(DeadLetterInstanceDecisionHandler)
            }, cancellationToken);
        }

        private static bool IsHardTerminal(OrchestrationInstanceStatus status)
            => status is OrchestrationInstanceStatus.Completed
                or OrchestrationInstanceStatus.CompletedWithErrors
                or OrchestrationInstanceStatus.Stopped
                or OrchestrationInstanceStatus.Compensating
                or OrchestrationInstanceStatus.Compensated
                or OrchestrationInstanceStatus.Aborted;
    }
}
