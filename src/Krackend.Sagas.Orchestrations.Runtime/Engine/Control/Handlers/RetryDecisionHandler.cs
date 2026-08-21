using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class RetryDecisionHandler : IDecisionHandler<RetryDecision>
    {
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly ITaskExecutionAttemptRepository _attemptRepository;
        private readonly ITaskDispatchRepository _dispatchRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IRemoteCommandDispatcher _dispatcher;
        private readonly IMessagingCommandSerializer _messagingCommandSerializer;
        private readonly IGetIngressConfigurationByArtifactAccessor _ingressConfigurationAccessor;

        public RetryDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            ITaskExecutionRepository taskRepository,
            ITaskExecutionAttemptRepository attemptRepository,
            ITaskDispatchRepository dispatchRepository,
            IExecutionTransitionRepository transitionRepository,
            IRemoteCommandDispatcher dispatcher,
            IMessagingCommandSerializer messagingCommandSerializer,
            IGetIngressConfigurationByArtifactAccessor ingressConfigurationAccessor)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
            _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messagingCommandSerializer = messagingCommandSerializer ?? throw new ArgumentNullException(nameof(messagingCommandSerializer));
            _ingressConfigurationAccessor = ingressConfigurationAccessor ?? throw new ArgumentNullException(nameof(ingressConfigurationAccessor));
        }

        public async Task HandleAsync(RetryDecision decision, CancellationToken cancellationToken = default)
        {
            if (decision.Task.Kind != TaskKind.Messaging)
            {
                throw new NotSupportedException($"Task kind '{decision.Task.Kind}' is not supported by retry dispatch.");
            }

            var messagingConfiguration = decision.Task.Configuration as MessagingTaskConfigurationArtifact
                ?? throw new InvalidOperationException($"Task '{decision.Task.Key}' does not contain a messaging configuration.");

            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var taskExecution = await _taskRepository.GetById(decision.TaskExecutionId, cancellationToken);
            var replyAddress = await ResolveBackchannelReplyAddressAsync(instance, cancellationToken);
            var attemptNumber = taskExecution.LastAttemptNumber + 1;
            var attempt = new TaskExecutionAttempt
            {
                Id = Id.New(),
                TaskExecutionId = taskExecution.Id,
                AttemptNumber = attemptNumber,
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
                DispatchStatus = "Enqueued",
                CommandId = Id.New().ToString(),
                CorrelationId = taskExecution.CorrelationId,
                ScheduledOnUtc = now
            };
            attempt.DispatchId = dispatch.Id;

            taskExecution.Status = TaskExecutionStatus.Running;
            taskExecution.LastAttemptNumber = attemptNumber;
            taskExecution.FailedOnUtc = null;
            taskExecution.WaitingSinceUtc = null;
            instance.Status = OrchestrationInstanceStatus.Running;
            instance.CurrentStageKey = decision.StageKey;
            instance.CurrentTaskKey = decision.Task.Key;
            instance.FailedOnUtc = null;
            instance.ErrorSummary = null;
            instance.LastUpdatedOnUtc = now;

            await _attemptRepository.Create(attempt, cancellationToken);
            await _dispatchRepository.Create(dispatch, cancellationToken);
            await _taskRepository.Update(taskExecution, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = decision.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskRetryEnqueued",
                FromStatus = TaskExecutionStatus.Failed.ToString(),
                ToStatus = dispatch.DispatchStatus,
                OccurredOnUtc = now,
                Message = $"Retry attempt {attemptNumber} enqueued for task '{decision.Task.Key}'.",
                Payload = string.IsNullOrWhiteSpace(decision.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(decision.Payload),
                ProducedBy = nameof(RetryDecisionHandler)
            }, cancellationToken);

            await QueueRetryAsync(
                decision,
                messagingConfiguration,
                instance,
                taskExecution,
                attempt,
                dispatch,
                replyAddress,
                cancellationToken);
        }

        private async Task QueueRetryAsync(
            RetryDecision decision,
            MessagingTaskConfigurationArtifact messagingConfiguration,
            OrchestrationInstance instance,
            TaskExecution taskExecution,
            TaskExecutionAttempt attempt,
            TaskDispatch dispatch,
            OrchestrationReplyAddress replyAddress,
            CancellationToken cancellationToken)
        {
            try
            {
                if (decision.Task.DispatchType != TaskDispatchType.FireAndForget && replyAddress is null)
                {
                    throw new InvalidOperationException(
                        $"Task '{decision.Task.Key}' requires a backchannel reply address, but none was projected for artifact '{instance.RuntimeOrchestrationArtifactId}'.");
                }

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
                    SettingsPayload = _messagingCommandSerializer.Serialize(messagingCommand),
                    OrchestrationInstanceId = instance.Id.ToString(),
                    StageExecutionId = decision.StageExecutionId.ToString(),
                    TaskExecutionId = taskExecution.Id.ToString(),
                    TaskExecutionAttemptId = attempt.Id.ToString(),
                    DispatchId = dispatch.Id.ToString(),
                    StageKey = decision.StageKey,
                    TaskKey = decision.Task.Key,
                    AwaitResponse = decision.Task.DispatchType != TaskDispatchType.FireAndForget,
                    MessageMetadata = new OrchestrationMessageMetadata
                    {
                        SagaId = instance.CorrelationId,
                        OrchestrationInstanceId = instance.Id.ToString(),
                        CurrentStage = decision.StageKey,
                        CurrentTasks = [decision.Task.Key],
                        CorrelationId = taskExecution.CorrelationId,
                        TaskExecutionId = taskExecution.Id.ToString(),
                        DispatchId = dispatch.Id.ToString(),
                        Attempt = attempt.AttemptNumber,
                        ReplyAddress = replyAddress
                    }
                }, cancellationToken);
            }
            catch (Exception exception)
            {
                var failedOnUtc = DateTime.UtcNow;
                dispatch.DispatchStatus = "Failed";
                dispatch.FailedOnUtc = failedOnUtc;
                dispatch.FailureReason = exception.Message;
                taskExecution.Status = TaskExecutionStatus.Failed;
                taskExecution.FailedOnUtc = failedOnUtc;
                attempt.Status = TaskExecutionStatus.Failed;
                attempt.FailedOnUtc = failedOnUtc;
                attempt.ErrorMessage = exception.Message;
                instance.Status = decision.Task.OnErrorPolicy == OnErrorPolicy.Continue
                    ? OrchestrationInstanceStatus.Running
                    : OrchestrationInstanceStatus.Failed;
                instance.FailedOnUtc = decision.Task.OnErrorPolicy == OnErrorPolicy.Continue ? null : failedOnUtc;
                instance.ErrorSummary = exception.Message;
                instance.LastUpdatedOnUtc = failedOnUtc;

                await _dispatchRepository.Update(dispatch, cancellationToken);
                await _attemptRepository.Update(attempt, cancellationToken);
                await _taskRepository.Update(taskExecution, cancellationToken);
                await _instanceRepository.Update(instance, cancellationToken);
                await _transitionRepository.Create(new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    StageExecutionId = decision.StageExecutionId,
                    TaskExecutionId = taskExecution.Id,
                    TaskExecutionAttemptId = attempt.Id,
                    TransitionType = "TaskRetryQueueFailed",
                    FromStatus = "Enqueued",
                    ToStatus = TaskExecutionStatus.Failed.ToString(),
                    OccurredOnUtc = failedOnUtc,
                    Message = exception.Message,
                    ProducedBy = nameof(RetryDecisionHandler)
                }, cancellationToken);
            }
        }

        private async Task<OrchestrationReplyAddress> ResolveBackchannelReplyAddressAsync(
            OrchestrationInstance instance,
            CancellationToken cancellationToken)
        {
            var configurations = await _ingressConfigurationAccessor.GetConfigurationAsync(
                instance.RuntimeOrchestrationArtifactId.ToString(),
                cancellationToken);
            var backchannel = configurations.FirstOrDefault(x =>
                x.IngressKind == IngressKind.Backchannel &&
                x.IngressTransport == IngressTransport.Messaging);

            return backchannel is null
                ? null
                : new OrchestrationReplyAddress
                {
                    Transport = OrchestrationTransportNames.Messaging,
                    SettingsPayload = backchannel.SettingsPayload
                };
        }
    }
}
