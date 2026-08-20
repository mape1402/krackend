using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class DispatchTaskDecisionHandler : IDecisionHandler<DispatchTaskDecision>
    {
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly ITaskExecutionAttemptRepository _attemptRepository;
        private readonly ITaskDispatchRepository _dispatchRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IRemoteCommandDispatcher _dispatcher;
        private readonly IMessagingCommandSerializer _messagingCommandSerializer;
        private readonly IInstanceMetadataSetter _metadataSetter;

        public DispatchTaskDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            ITaskExecutionRepository taskRepository,
            ITaskExecutionAttemptRepository attemptRepository,
            ITaskDispatchRepository dispatchRepository,
            IExecutionTransitionRepository transitionRepository,
            IRemoteCommandDispatcher dispatcher,
            IMessagingCommandSerializer messagingCommandSerializer,
            IInstanceMetadataSetter metadataSetter)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
            _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messagingCommandSerializer = messagingCommandSerializer ?? throw new ArgumentNullException(nameof(messagingCommandSerializer));
            _metadataSetter = metadataSetter ?? throw new ArgumentNullException(nameof(metadataSetter));
        }

        public async Task HandleAsync(DispatchTaskDecision decision, CancellationToken cancellationToken = default)
        {
            if (decision.Task.Kind != TaskKind.Messaging)
            {
                throw new NotSupportedException($"Task kind '{decision.Task.Kind}' is not supported by the minimal runtime engine.");
            }

            var messagingConfiguration = decision.Task.Configuration as MessagingTaskConfigurationArtifact
                ?? throw new InvalidOperationException($"Task '{decision.Task.Key}' does not contain a messaging configuration.");

            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var taskExecution = new TaskExecution
            {
                Id = Id.New(),
                OrchestrationInstanceId = decision.InstanceId,
                StageExecutionId = decision.StageExecutionId,
                TaskKey = decision.Task.Key,
                TaskKind = decision.Task.Kind,
                ExecutionMode = decision.Task.ExecutionMode,
                ParallelGroupId = decision.Task.ParallelGroupId,
                Status = TaskExecutionStatus.Running,
                WasSkipped = false,
                SkipReason = string.Empty,
                ExecutionConditionResult = true,
                OnErrorPolicy = decision.Task.OnErrorPolicy,
                AwaitResponse = decision.Task.DispatchType != TaskDispatchType.FireAndForget,
                StartedOnUtc = now,
                LastAttemptNumber = 1,
                CorrelationId = string.IsNullOrWhiteSpace(instance.CorrelationId) ? Id.New().ToString() : instance.CorrelationId
            };
            var attempt = new TaskExecutionAttempt
            {
                Id = Id.New(),
                TaskExecutionId = taskExecution.Id,
                AttemptNumber = 1,
                Status = TaskExecutionStatus.Running,
                StartedOnUtc = now,
                RequestPayload = string.IsNullOrWhiteSpace(decision.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(decision.Payload)
            };
            var dispatch = new TaskDispatch
            {
                Id = Id.New(),
                TaskExecutionAttemptId = attempt.Id,
                DispatchType = decision.Task.DispatchType.ToString(),
                Destination = $"{messagingConfiguration.Topic}:{messagingConfiguration.Version}",
                RequestPayload = string.IsNullOrWhiteSpace(decision.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(decision.Payload),
                DispatchStatus = "Scheduled",
                CommandId = Id.New().ToString(),
                CorrelationId = taskExecution.CorrelationId,
                ScheduledOnUtc = now
            };
            attempt.DispatchId = dispatch.Id;

            instance.Status = OrchestrationInstanceStatus.Running;
            instance.CurrentStageKey = decision.StageKey;
            instance.CurrentTaskKey = decision.Task.Key;
            instance.LastUpdatedOnUtc = now;

            await _taskRepository.Create(taskExecution, cancellationToken);
            await _attemptRepository.Create(attempt, cancellationToken);
            await _dispatchRepository.Create(dispatch, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);

            _metadataSetter.Set(new InstanceMetadata
            {
                SagaId = instance.CorrelationId,
                OrchestrationInstanceId = instance.Id.ToString(),
                CurrentStage = decision.StageKey,
                CurrentTasks = [decision.Task.Key],
                CorrelationId = taskExecution.CorrelationId,
                TaskExecutionId = taskExecution.Id.ToString(),
                DispatchId = dispatch.Id.ToString(),
                Attempt = attempt.AttemptNumber
            });

            var messagingCommand = new MessagingCommand
            {
                Topic = messagingConfiguration.Topic,
                Version = messagingConfiguration.Version.ToString(),
                Payload = decision.Payload
            };
            await _dispatcher.DispatchAsync(new RemoteCommand
            {
                Payload = decision.Payload,
                RemoteCommandTransport = RemoteCommandTransport.Messaging,
                SettingsPayload = _messagingCommandSerializer.Serialize(messagingCommand)
            }, cancellationToken);

            var sentOnUtc = DateTime.UtcNow;
            dispatch.DispatchStatus = decision.Task.DispatchType == TaskDispatchType.FireAndForget ? "Completed" : "WaitingResponse";
            dispatch.SentOnUtc = sentOnUtc;

            if (decision.Task.DispatchType == TaskDispatchType.FireAndForget)
            {
                taskExecution.Status = TaskExecutionStatus.Completed;
                taskExecution.CompletedOnUtc = sentOnUtc;
                attempt.Status = TaskExecutionStatus.Completed;
                attempt.CompletedOnUtc = sentOnUtc;
            }
            else
            {
                taskExecution.Status = TaskExecutionStatus.WaitingResponse;
                taskExecution.WaitingSinceUtc = sentOnUtc;
                attempt.Status = TaskExecutionStatus.WaitingResponse;
                attempt.WaitingSinceUtc = sentOnUtc;
                instance.Status = OrchestrationInstanceStatus.Waiting;
                instance.WaitingSinceUtc = sentOnUtc;
            }

            instance.LastUpdatedOnUtc = sentOnUtc;
            await _taskRepository.Update(taskExecution, cancellationToken);
            await _attemptRepository.Update(attempt, cancellationToken);
            await _dispatchRepository.Update(dispatch, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = decision.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatched",
                FromStatus = TaskExecutionStatus.Pending.ToString(),
                ToStatus = taskExecution.Status.ToString(),
                OccurredOnUtc = sentOnUtc,
                Message = $"Task '{decision.Task.Key}' dispatched to messaging topic '{messagingConfiguration.Topic}'.",
                Payload = string.IsNullOrWhiteSpace(decision.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(decision.Payload),
                ProducedBy = nameof(DispatchTaskDecisionHandler)
            }, cancellationToken);
        }
    }
}
