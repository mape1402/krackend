using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class CompleteCallbackDecisionHandler : IDecisionHandler<CompleteCallbackDecision>
    {
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly ITaskExecutionAttemptRepository _attemptRepository;
        private readonly ITaskDispatchRepository _dispatchRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;

        public CompleteCallbackDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            ITaskExecutionRepository taskRepository,
            ITaskExecutionAttemptRepository attemptRepository,
            ITaskDispatchRepository dispatchRepository,
            IExecutionTransitionRepository transitionRepository)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
            _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        }

        public async Task HandleAsync(CompleteCallbackDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var task = await _taskRepository.GetById(decision.TaskExecutionId, cancellationToken);
            var dispatch = await _dispatchRepository.GetById(decision.DispatchId, cancellationToken);
            var attempt = await _attemptRepository.GetByDispatchId(decision.DispatchId, cancellationToken);

            dispatch.DispatchStatus = decision.Succeeded ? "Acknowledged" : "Failed";
            dispatch.AcknowledgedOnUtc = decision.Succeeded ? now : null;
            dispatch.FailedOnUtc = decision.Succeeded ? null : now;
            dispatch.FailureReason = decision.Succeeded ? null : decision.ErrorMessage;
            task.Status = decision.Succeeded ? TaskExecutionStatus.Completed : TaskExecutionStatus.Failed;
            task.CompletedOnUtc = decision.Succeeded ? now : null;
            task.FailedOnUtc = decision.Succeeded ? null : now;
            task.WaitingSinceUtc = null;
            attempt.Status = decision.Succeeded ? TaskExecutionStatus.Completed : TaskExecutionStatus.Failed;
            attempt.CompletedOnUtc = decision.Succeeded ? now : null;
            attempt.FailedOnUtc = decision.Succeeded ? null : now;
            attempt.WaitingSinceUtc = null;
            attempt.ResponsePayload = string.IsNullOrWhiteSpace(decision.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(decision.Payload);
            attempt.ErrorCode = decision.Succeeded ? null : decision.ErrorCode;
            attempt.ErrorMessage = decision.Succeeded ? null : decision.ErrorMessage;
            if (!string.IsNullOrWhiteSpace(decision.EnvelopePayload))
            {
                attempt.Metadata["OrchestrationResponseEnvelope"] = System.Text.Json.Nodes.JsonNode.Parse(decision.EnvelopePayload);
            }

            instance.Status = decision.Succeeded || task.OnErrorPolicy == OnErrorPolicy.Continue
                ? OrchestrationInstanceStatus.Running
                : OrchestrationInstanceStatus.Failed;
            instance.FailedOnUtc = decision.Succeeded || task.OnErrorPolicy == OnErrorPolicy.Continue ? null : now;
            instance.ErrorSummary = decision.Succeeded ? null : decision.ErrorMessage;
            instance.WaitingSinceUtc = null;
            instance.LastUpdatedOnUtc = now;

            await _dispatchRepository.Update(dispatch, cancellationToken);
            await _attemptRepository.Update(attempt, cancellationToken);
            await _taskRepository.Update(task, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = decision.Succeeded ? "TaskCallbackCompleted" : "TaskCallbackFailed",
                FromStatus = TaskExecutionStatus.WaitingResponse.ToString(),
                ToStatus = task.Status.ToString(),
                OccurredOnUtc = now,
                Message = decision.Succeeded
                    ? $"Callback completed task '{task.TaskKey}'."
                    : $"Callback failed task '{task.TaskKey}': {decision.ErrorMessage}",
                Payload = string.IsNullOrWhiteSpace(decision.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(decision.Payload),
                ProducedBy = nameof(CompleteCallbackDecisionHandler)
            }, cancellationToken);
        }
    }
}
