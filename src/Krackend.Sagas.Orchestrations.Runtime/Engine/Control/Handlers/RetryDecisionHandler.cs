using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using System.Text.Json.Nodes;

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
        private readonly IOrchestrationPayloadState _payloadState;
        private readonly ITaskDispatchRequestPayloadPreparer _requestPayloadPreparer;

        public RetryDecisionHandler(
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

        public async Task HandleAsync(RetryDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var taskExecution = await _taskRepository.GetById(decision.TaskExecutionId, cancellationToken);
            if (!CanRetryCurrentTaskState(taskExecution, decision.Task))
            {
                return;
            }

            if (decision.Task.Kind != TaskKind.Messaging)
            {
                await MarkRetryConfigurationFailedAsync(
                    decision,
                    instance,
                    taskExecution,
                    now,
                    $"Task kind '{decision.Task.Kind}' is not supported by retry dispatch.",
                    cancellationToken);
                return;
            }

            if (decision.Task.Configuration is not MessagingTaskConfigurationArtifact messagingConfiguration)
            {
                await MarkRetryConfigurationFailedAsync(
                    decision,
                    instance,
                    taskExecution,
                    now,
                    $"Task '{decision.Task.Key}' does not contain a messaging configuration.",
                    cancellationToken);
                return;
            }

            var replyAddress = await ResolveBackchannelReplyAddressAsync(instance, cancellationToken);
            var attemptNumber = taskExecution.LastAttemptNumber + 1;
            var scheduledOnUtc = ResolveRetryScheduledOnUtc(taskExecution, decision.Task, now);
            var hasDeferredDispatch = scheduledOnUtc > DateTimeOffset.UtcNow;
            var requestPayload = ParsePayload(decision.Payload);
            var attempt = new TaskExecutionAttempt
            {
                Id = Id.New(),
                TaskExecutionId = taskExecution.Id,
                AttemptNumber = attemptNumber,
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
                ScheduledOnUtc = scheduledOnUtc.UtcDateTime
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
            dispatch.DispatchStatus = hasDeferredDispatch ? "Scheduled" : "Enqueued";
            instance.LastUpdatedOnUtc = queuedOnUtc;
            await _attemptRepository.Update(attempt, cancellationToken);
            await _dispatchRepository.Update(dispatch, cancellationToken);
            await _taskRepository.Update(taskExecution, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = decision.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = hasDeferredDispatch ? "TaskRetryScheduled" : "TaskRetryEnqueued",
                FromStatus = TaskExecutionStatus.Failed.ToString(),
                ToStatus = dispatch.DispatchStatus,
                OccurredOnUtc = queuedOnUtc,
                Message = hasDeferredDispatch
                    ? $"Retry attempt {attemptNumber} scheduled for task '{decision.Task.Key}' at {scheduledOnUtc:O}."
                    : $"Retry attempt {attemptNumber} enqueued for task '{decision.Task.Key}'.",
                Payload = requestPayload?.DeepClone(),
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
                hasDeferredDispatch ? scheduledOnUtc : null,
                requestPayload?.ToJsonString(),
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
            DateTimeOffset? scheduledOnUtc,
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
                    ScheduledOnUtc = scheduledOnUtc,
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

        private static DateTimeOffset ResolveRetryScheduledOnUtc(
            TaskExecution failedTask,
            TaskArtifact taskArtifact,
            DateTime now)
        {
            var retryPolicy = ResolveRetryPolicy(failedTask, taskArtifact);
            if (retryPolicy?.Strategy is not FixedRetryStrategyArtifact fixedRetry ||
                fixedRetry.Delay.Value <= TimeSpan.Zero)
            {
                return new DateTimeOffset(now, TimeSpan.Zero);
            }

            return new DateTimeOffset(now, TimeSpan.Zero).Add(fixedRetry.Delay.Value);
        }

        private static RetryPolicyArtifact ResolveRetryPolicy(
            TaskExecution failedTask,
            TaskArtifact taskArtifact)
        {
            if (failedTask.Status == TaskExecutionStatus.TimedOut &&
                taskArtifact?.TimeoutPolicy?.TimeoutBehaviorPolicy is ReconcileTimeoutBehaviorPolicyArtifact reconcile)
            {
                return reconcile.RetryPolicy;
            }

            return taskArtifact?.RetryPolicy;
        }

        private static bool CanRetryCurrentTaskState(
            TaskExecution taskExecution,
            TaskArtifact taskArtifact)
        {
            if (taskExecution.Status is not (TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut))
            {
                return false;
            }

            var retryPolicy = ResolveRetryPolicy(taskExecution, taskArtifact);
            return retryPolicy is not null &&
                taskExecution.LastAttemptNumber <= retryPolicy.MaxRetries;
        }

        private static JsonNode ParsePayload(string payload)
            => string.IsNullOrWhiteSpace(payload) ? null : JsonNode.Parse(payload);

        private static string GetSagaId(OrchestrationInstance instance)
            => string.IsNullOrWhiteSpace(instance.SagaId)
                ? instance.Id.ToString()
                : instance.SagaId;

        private async Task MarkPreparationFailedAsync(
            RetryDecision decision,
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
                TransitionType = "TaskRetryPreparationFailed",
                FromStatus = TaskExecutionStatus.Running.ToString(),
                ToStatus = TaskExecutionStatus.Failed.ToString(),
                OccurredOnUtc = failedOnUtc,
                Message = exception.Message,
                ProducedBy = nameof(RetryDecisionHandler)
            }, cancellationToken);
        }

        private async Task MarkRetryConfigurationFailedAsync(
            RetryDecision decision,
            OrchestrationInstance instance,
            TaskExecution taskExecution,
            DateTime failedOnUtc,
            string message,
            CancellationToken cancellationToken)
        {
            taskExecution.Status = TaskExecutionStatus.Failed;
            taskExecution.FailedOnUtc = failedOnUtc;
            taskExecution.Metadata["RetrySuppressed"] = JsonValue.Create(true);
            taskExecution.Metadata["RetryConfigurationError"] = JsonValue.Create(message);
            instance.Status = decision.Task.OnErrorPolicy == OnErrorPolicy.Continue
                ? OrchestrationInstanceStatus.Running
                : OrchestrationInstanceStatus.Failed;
            instance.FailedOnUtc = decision.Task.OnErrorPolicy == OnErrorPolicy.Continue ? null : failedOnUtc;
            instance.ErrorSummary = message;
            instance.LastUpdatedOnUtc = failedOnUtc;

            await _taskRepository.Update(taskExecution, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = decision.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TransitionType = "TaskRetryConfigurationFailed",
                FromStatus = TaskExecutionStatus.Failed.ToString(),
                ToStatus = taskExecution.Status.ToString(),
                OccurredOnUtc = failedOnUtc,
                Message = message,
                ProducedBy = nameof(RetryDecisionHandler)
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
