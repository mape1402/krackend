using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using System.Text.Json.Nodes;

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
        private readonly IGetIngressConfigurationByArtifactAccessor _ingressConfigurationAccessor;
        private readonly IOrchestrationPayloadState _payloadState;
        private readonly ITaskDispatchRequestPayloadPreparer _requestPayloadPreparer;

        public DispatchTaskDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            ITaskExecutionRepository taskRepository,
            ITaskExecutionAttemptRepository attemptRepository,
            ITaskDispatchRepository dispatchRepository,
            IExecutionTransitionRepository transitionRepository,
            IRemoteCommandDispatcher dispatcher,
            IMessagingCommandSerializer messagingCommandSerializer,
            IGetIngressConfigurationByArtifactAccessor ingressConfigurationAccessor,
            IOrchestrationPayloadState payloadState,
            ITaskDispatchRequestPayloadPreparer requestPayloadPreparer)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
            _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messagingCommandSerializer = messagingCommandSerializer ?? throw new ArgumentNullException(nameof(messagingCommandSerializer));
            _ingressConfigurationAccessor = ingressConfigurationAccessor ?? throw new ArgumentNullException(nameof(ingressConfigurationAccessor));
            _payloadState = payloadState ?? throw new ArgumentNullException(nameof(payloadState));
            _requestPayloadPreparer = requestPayloadPreparer ?? throw new ArgumentNullException(nameof(requestPayloadPreparer));
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
            var replyAddress = await ResolveBackchannelReplyAddressAsync(instance, cancellationToken);
            var requestPayload = ParsePayload(decision.Payload);
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
                RequestPayload = requestPayload?.DeepClone()
            };
            var dispatch = new TaskDispatch
            {
                Id = Id.New(),
                TaskExecutionAttemptId = attempt.Id,
                DispatchType = decision.Task.DispatchType.ToString(),
                Destination = $"{messagingConfiguration.Topic}:{messagingConfiguration.Version}",
                RequestPayload = requestPayload?.DeepClone(),
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
            instance.SnapshotPayload = _payloadState.ApplyTaskRequestPayload(
                instance,
                decision.StageKey,
                decision.Task.Key,
                requestPayload);

            taskExecution.Metadata["Timeout"] = System.Text.Json.Nodes.JsonValue.Create(decision.Task.TimeoutPolicy?.Timeout.ToString() ?? string.Empty);

            await _taskRepository.Create(taskExecution, cancellationToken);
            await _attemptRepository.Create(attempt, cancellationToken);
            await _dispatchRepository.Create(dispatch, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);

            try
            {
                var preparation = await _requestPayloadPreparer.PrepareAsync(
                    new TaskDispatchRequestPayloadPreparationRequest
                    {
                        Instance = instance,
                        StageKey = decision.StageKey,
                        Task = decision.Task,
                        MessagingConfiguration = messagingConfiguration,
                        Payload = decision.Payload
                    },
                    cancellationToken);

                requestPayload = preparation.Payload;

                attempt.RequestPayload = requestPayload?.DeepClone();
                dispatch.RequestPayload = requestPayload?.DeepClone();
                instance.SnapshotPayload = _payloadState.ApplyTaskRequestPayload(
                    instance,
                    decision.StageKey,
                    decision.Task.Key,
                    requestPayload);
            }
            catch (TaskDispatchPreparationException exception)
            {
                await MarkPreparationFailedAsync(
                    decision,
                    instance,
                    taskExecution,
                    attempt,
                    dispatch,
                    exception,
                    cancellationToken);
                return;
            }

            var queuedOnUtc = DateTime.UtcNow;
            dispatch.DispatchStatus = "Enqueued";
            instance.LastUpdatedOnUtc = queuedOnUtc;
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
                TransitionType = "TaskDispatchEnqueued",
                FromStatus = TaskExecutionStatus.Pending.ToString(),
                ToStatus = dispatch.DispatchStatus,
                OccurredOnUtc = queuedOnUtc,
                Message = $"Task '{decision.Task.Key}' dispatch enqueued for transport '{RemoteCommandTransport.Messaging}'.",
                Payload = requestPayload?.DeepClone(),
                ProducedBy = nameof(DispatchTaskDecisionHandler)
            }, cancellationToken);

            await TryQueueDispatchAsync(
                decision,
                messagingConfiguration,
                instance,
                taskExecution,
                attempt,
                dispatch,
                replyAddress,
                requestPayload?.ToJsonString(),
                cancellationToken);
        }

        private async Task<bool> TryQueueDispatchAsync(
            DispatchTaskDecision decision,
            MessagingTaskConfigurationArtifact messagingConfiguration,
            OrchestrationInstance instance,
            TaskExecution taskExecution,
            TaskExecutionAttempt attempt,
            TaskDispatch dispatch,
            OrchestrationReplyAddress replyAddress,
            string commandPayload,
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
                    Payload = commandPayload
                };

                await _dispatcher.DispatchAsync(new RemoteCommand
                {
                    Payload = commandPayload,
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
                        SagaId = GetSagaId(instance),
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

                return true;
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
                attempt.ErrorCode = "CommandDispatchFailed";
                attempt.ErrorMessage = exception.Message;
                instance.Status = decision.Task.OnErrorPolicy == OnErrorPolicy.Continue
                    ? OrchestrationInstanceStatus.Running
                    : OrchestrationInstanceStatus.Failed;
                instance.FailedOnUtc = decision.Task.OnErrorPolicy == OnErrorPolicy.Continue ? null : failedOnUtc;
                instance.ErrorSummary = exception.Message;
                instance.LastUpdatedOnUtc = failedOnUtc;

                await _dispatchRepository.Update(dispatch, cancellationToken);
                await _taskRepository.Update(taskExecution, cancellationToken);
                await _attemptRepository.Update(attempt, cancellationToken);
                await _instanceRepository.Update(instance, cancellationToken);
                await _transitionRepository.Create(new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    StageExecutionId = decision.StageExecutionId,
                    TaskExecutionId = taskExecution.Id,
                    TaskExecutionAttemptId = attempt.Id,
                    TransitionType = "TaskDispatchQueueFailed",
                    FromStatus = TaskExecutionStatus.Running.ToString(),
                    ToStatus = TaskExecutionStatus.Failed.ToString(),
                    OccurredOnUtc = failedOnUtc,
                    Message = exception.Message,
                    ProducedBy = nameof(DispatchTaskDecisionHandler)
                }, cancellationToken);
                return false;
            }
        }

        private static JsonNode ParsePayload(string payload)
            => string.IsNullOrWhiteSpace(payload) ? null : JsonNode.Parse(payload);

        private static string GetSagaId(OrchestrationInstance instance)
            => string.IsNullOrWhiteSpace(instance.SagaId)
                ? instance.Id.ToString()
                : instance.SagaId;

        private async Task MarkPreparationFailedAsync(
            DispatchTaskDecision decision,
            OrchestrationInstance instance,
            TaskExecution taskExecution,
            TaskExecutionAttempt attempt,
            TaskDispatch dispatch,
            TaskDispatchPreparationException exception,
            CancellationToken cancellationToken)
        {
            var failedOnUtc = DateTime.UtcNow;
            dispatch.DispatchStatus = "Failed";
            dispatch.FailedOnUtc = failedOnUtc;
            dispatch.FailureReason = exception.Message;
            taskExecution.Status = TaskExecutionStatus.Failed;
            taskExecution.FailedOnUtc = failedOnUtc;
            attempt.Status = TaskExecutionStatus.Failed;
            attempt.FailedOnUtc = failedOnUtc;
            attempt.ErrorCode = exception.ErrorCode;
            attempt.ErrorMessage = exception.Message;
            attempt.Metadata["PreparationErrorCode"] = JsonValue.Create(exception.ErrorCode);

            foreach (var diagnostic in exception.Diagnostics)
            {
                attempt.Metadata[$"Preparation.{diagnostic.Key}"] = diagnostic.Value?.DeepClone();
            }

            instance.Status = decision.Task.OnErrorPolicy == OnErrorPolicy.Continue
                ? OrchestrationInstanceStatus.Running
                : OrchestrationInstanceStatus.Failed;
            instance.FailedOnUtc = decision.Task.OnErrorPolicy == OnErrorPolicy.Continue ? null : failedOnUtc;
            instance.ErrorSummary = exception.Message;
            instance.LastUpdatedOnUtc = failedOnUtc;

            await _dispatchRepository.Update(dispatch, cancellationToken);
            await _taskRepository.Update(taskExecution, cancellationToken);
            await _attemptRepository.Update(attempt, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = decision.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchPreparationFailed",
                FromStatus = TaskExecutionStatus.Running.ToString(),
                ToStatus = TaskExecutionStatus.Failed.ToString(),
                OccurredOnUtc = failedOnUtc,
                Message = exception.Message,
                ProducedBy = nameof(DispatchTaskDecisionHandler)
            }, cancellationToken);
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
