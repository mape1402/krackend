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

            dispatch.DispatchStatus = "Acknowledged";
            dispatch.AcknowledgedOnUtc = now;
            task.Status = TaskExecutionStatus.Completed;
            task.CompletedOnUtc = now;
            task.WaitingSinceUtc = null;
            attempt.Status = TaskExecutionStatus.Completed;
            attempt.CompletedOnUtc = now;
            attempt.WaitingSinceUtc = null;
            attempt.ResponsePayload = string.IsNullOrWhiteSpace(decision.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(decision.Payload);
            instance.Status = OrchestrationInstanceStatus.Running;
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
                TransitionType = "TaskCallbackCompleted",
                FromStatus = TaskExecutionStatus.WaitingResponse.ToString(),
                ToStatus = TaskExecutionStatus.Completed.ToString(),
                OccurredOnUtc = now,
                Message = $"Callback completed task '{task.TaskKey}'.",
                Payload = string.IsNullOrWhiteSpace(decision.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(decision.Payload),
                ProducedBy = nameof(CompleteCallbackDecisionHandler)
            }, cancellationToken);
        }
    }
}
