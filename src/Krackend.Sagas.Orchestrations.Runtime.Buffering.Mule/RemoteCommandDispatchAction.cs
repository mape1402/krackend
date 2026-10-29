using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    /// <summary>
    /// Executes queued remote command dispatches through the configured runtime transport executor.
    /// </summary>
    [MuleAction(MuleActionKeys.RemoteCommandDispatchAction)]
    public sealed class RemoteCommandDispatchAction : IMuleAction<RemoteCommand>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOrchestrationMessageMetadataSetter _messageMetadataSetter;
        private readonly IOrchestrationPropagationMetadataSetter? _propagationMetadataSetter;
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly ITaskExecutionAttemptRepository _attemptRepository;
        private readonly ITaskDispatchRepository _dispatchRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IMuleTerminalFailureMarker _terminalFailureMarker;
        private readonly IOrchestrationInstanceCoordinator? _coordinator;
        private readonly IRuntimeAdmissionController? _admissionController;
        private readonly IRuntimeStorageUnitOfWork? _storageUnitOfWork;
        private readonly ITaskResponseTimeoutDeadlineResolver? _timeoutDeadlineResolver;
        private readonly ITaskResponseTimeoutScheduler? _timeoutScheduler;
        private readonly ILogger<RemoteCommandDispatchAction> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteCommandDispatchAction"/> class.
        /// </summary>
        public RemoteCommandDispatchAction(
            IServiceProvider serviceProvider,
            IOrchestrationMessageMetadataSetter messageMetadataSetter,
            IOrchestrationInstanceRepository instanceRepository,
            ITaskExecutionRepository taskRepository,
            ITaskExecutionAttemptRepository attemptRepository,
            ITaskDispatchRepository dispatchRepository,
            IExecutionTransitionRepository transitionRepository,
            IMuleTerminalFailureMarker terminalFailureMarker,
            IOrchestrationPropagationMetadataSetter? propagationMetadataSetter = null,
            IOrchestrationInstanceCoordinator? coordinator = null,
            IRuntimeAdmissionController? admissionController = null,
            IRuntimeStorageUnitOfWork? storageUnitOfWork = null,
            ITaskResponseTimeoutDeadlineResolver? timeoutDeadlineResolver = null,
            ITaskResponseTimeoutScheduler? timeoutScheduler = null,
            ILogger<RemoteCommandDispatchAction>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _messageMetadataSetter = messageMetadataSetter ?? throw new ArgumentNullException(nameof(messageMetadataSetter));
            _propagationMetadataSetter = propagationMetadataSetter;
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
            _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _terminalFailureMarker = terminalFailureMarker ?? throw new ArgumentNullException(nameof(terminalFailureMarker));
            _coordinator = coordinator;
            _admissionController = admissionController;
            _storageUnitOfWork = storageUnitOfWork;
            _timeoutDeadlineResolver = timeoutDeadlineResolver;
            _timeoutScheduler = timeoutScheduler;
            _logger = logger ?? NullLogger<RemoteCommandDispatchAction>.Instance;
        }

        /// <summary>
        /// Executes the queued remote command and updates runtime dispatch state.
        /// </summary>
        public async ValueTask ExecuteAsync(MuleActionContext<RemoteCommand> context, CancellationToken cancellationToken)
        {
            var command = context.Payload ?? throw new InvalidOperationException("Remote command payload cannot be empty.");
            if (_admissionController is not null)
            {
                await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.Dispatch, cancellationToken);
            }

            if (HasRuntimeDispatchState(command) && _coordinator is not null)
            {
                var instanceId = ParseId(command.OrchestrationInstanceId);
                var acquired = await _coordinator.TryExecuteAsync(
                    instanceId,
                    token => ExecuteCoreAsync(context, command, token),
                    cancellationToken);

                if (!acquired)
                {
                    throw new OrchestrationInstanceLeaseUnavailableException(instanceId);
                }

                return;
            }

            await ExecuteCoreAsync(context, command, cancellationToken);
        }

        private async Task ExecuteCoreAsync(
            MuleActionContext<RemoteCommand> context,
            RemoteCommand command,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!await IsCurrentDispatchAsync(command, cancellationToken))
                {
                    return;
                }

                await PersistCommandMetadataAsync(command, cancellationToken);

                var executor = _serviceProvider.GetKeyedService<IRemoteCommandExecutor>(command.RemoteCommandTransport)
                    ?? throw new RemoteCommandConfigurationException($"No remote command executor is configured for '{command.RemoteCommandTransport}'.");

                _messageMetadataSetter.Set(command.MessageMetadata ?? new OrchestrationMessageMetadata());
                _propagationMetadataSetter?.Set(command.PropagationMetadata ?? new OrchestrationPropagationMetadata());
                await executor.ExecuteAsync(command, cancellationToken);
                if (HasRuntimeDispatchState(command))
                {
                    if (await MarkDispatchPublishedAsync(command, DateTime.UtcNow, cancellationToken))
                    {
                        await ContinueFireAndForgetAsync(command, cancellationToken);
                    }
                }
            }
            catch (RemoteCommandConfigurationException exception)
            {
                if (HasRuntimeDispatchState(command))
                {
                    await MarkDispatchFailedAsync(command, exception, cancellationToken);
                }

                _terminalFailureMarker.MarkTerminal(context);
                throw;
            }
            catch (Exception exception)
            {
                var shouldRetry = true;
                if (HasRuntimeDispatchState(command))
                {
                    shouldRetry = await MarkDispatchRetryPendingAsync(command, exception, cancellationToken);
                }

                if (shouldRetry)
                {
                    throw;
                }
            }
        }

        private async Task<bool> IsCurrentDispatchAsync(
            RemoteCommand command,
            CancellationToken cancellationToken)
        {
            if (!HasRuntimeDispatchState(command))
            {
                return true;
            }

            var instanceId = ParseId(command.OrchestrationInstanceId);
            var taskId = ParseId(command.TaskExecutionId);
            var attemptId = ParseId(command.TaskExecutionAttemptId);
            var dispatchId = ParseId(command.DispatchId);
            var instance = await _instanceRepository.GetById(instanceId, cancellationToken);
            var task = await _taskRepository.GetById(taskId, cancellationToken);
            var attempt = await _attemptRepository.GetById(attemptId, cancellationToken);
            var dispatch = await _dispatchRepository.GetById(dispatchId, cancellationToken);

            if (instance.Id == instanceId &&
                task.OrchestrationInstanceId == instanceId &&
                attempt.TaskExecutionId == taskId &&
                dispatch.TaskExecutionAttemptId == attemptId &&
                !IsTerminal(instance.Status) &&
                task.Status == TaskExecutionStatus.Running &&
                attempt.Status == TaskExecutionStatus.Running &&
                (task.LastAttemptNumber == 0 || task.LastAttemptNumber == attempt.AttemptNumber) &&
                IsDispatchPending(dispatch.DispatchStatus))
            {
                return true;
            }

            _logger.LogDebug(
                "Suppressed stale durable dispatch '{DispatchId}' for saga '{SagaId}'.",
                dispatch.Id,
                instance.SagaId);
            return false;
        }

        private async Task PersistCommandMetadataAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            if (!HasRuntimeDispatchState(command))
            {
                return;
            }

            var metadata = RemoteCommandMetadataBuilder.Build(command.MessageMetadata, command.PropagationMetadata);
            var dispatch = await _dispatchRepository.GetById(ParseId(command.DispatchId), cancellationToken);
            foreach (var existing in dispatch.Metadata)
            {
                if (!metadata.ContainsKey(existing.Key))
                {
                    metadata[existing.Key] = existing.Value?.DeepClone();
                }
            }

            dispatch.Metadata = metadata;
            await _dispatchRepository.Update(dispatch, cancellationToken);
        }

        private async Task<bool> MarkDispatchPublishedAsync(
            RemoteCommand command,
            DateTime dispatchedOnUtc,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            if (command.AwaitResponse)
            {
                return await MarkAwaitedDispatchPublishedAsync(command, now, dispatchedOnUtc, cancellationToken);
            }

            var instance = await _instanceRepository.GetById(ParseId(command.OrchestrationInstanceId), cancellationToken);
            var task = await _taskRepository.GetById(ParseId(command.TaskExecutionId), cancellationToken);
            var attempt = await _attemptRepository.GetById(ParseId(command.TaskExecutionAttemptId), cancellationToken);
            var dispatch = await _dispatchRepository.GetById(ParseId(command.DispatchId), cancellationToken);

            if (IsFinished(dispatch.DispatchStatus))
            {
                await RecordIgnoredPublishedDispatchAsync(instance, task, attempt, dispatch, command, cancellationToken);
                return false;
            }

            var previousDispatchStatus = dispatch.DispatchStatus;
            dispatch.SentOnUtc = now;
            if (!string.Equals(dispatch.DispatchStatus, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                dispatch.FailedOnUtc = null;
                dispatch.FailureReason = null;
            }

            dispatch.DispatchStatus = "Completed";

            if (!IsTerminal(task.Status))
            {
                task.Status = TaskExecutionStatus.Completed;
                task.CompletedOnUtc = now;
            }

            if (!IsTerminal(attempt.Status))
            {
                attempt.Status = TaskExecutionStatus.Completed;
                attempt.CompletedOnUtc = now;
            }

            if (!IsTerminal(instance.Status))
            {
                instance.Status = OrchestrationInstanceStatus.Running;
                instance.WaitingSinceUtc = null;
            }

            if (!IsTerminal(instance.Status))
            {
                instance.LastUpdatedOnUtc = now;
            }

            await PersistStateMutationAsync(async () =>
            {
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
                    TransitionType = "TaskDispatched",
                    FromStatus = previousDispatchStatus,
                    ToStatus = task.Status.ToString(),
                    OccurredOnUtc = dispatchedOnUtc,
                    Message = $"Task '{task.TaskKey}' command published with transport '{command.RemoteCommandTransport}'.",
                    Payload = string.IsNullOrWhiteSpace(command.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(command.Payload),
                    ProducedBy = nameof(RemoteCommandDispatchAction)
                }, cancellationToken);
            }, cancellationToken);
            return true;
        }

        private async Task<bool> MarkAwaitedDispatchPublishedAsync(
            RemoteCommand command,
            DateTime sentOnUtc,
            DateTime dispatchedOnUtc,
            CancellationToken cancellationToken)
        {
            var dispatchId = ParseId(command.DispatchId);
            var instance = await _instanceRepository.GetById(ParseId(command.OrchestrationInstanceId), cancellationToken);
            var task = await _taskRepository.GetById(ParseId(command.TaskExecutionId), cancellationToken);
            var attempt = await _attemptRepository.GetById(ParseId(command.TaskExecutionAttemptId), cancellationToken);
            var dispatch = await _dispatchRepository.GetById(dispatchId, cancellationToken);
            if (IsFinished(dispatch.DispatchStatus))
            {
                await RecordIgnoredPublishedDispatchAsync(instance, task, attempt, dispatch, command, cancellationToken);
                return false;
            }

            var previousDispatchStatus = dispatch.DispatchStatus;
            dispatch.SentOnUtc = sentOnUtc;
            dispatch.DispatchStatus = "WaitingResponse";
            dispatch.FailedOnUtc = null;
            dispatch.FailureReason = null;
            if (!IsTerminal(task.Status))
            {
                task.Status = TaskExecutionStatus.WaitingResponse;
                task.WaitingSinceUtc = sentOnUtc;
                task.ResponseDeadlineUtc = await ResolveResponseDeadlineAsync(instance, task, sentOnUtc, cancellationToken);
                task.TimeoutActionId = null;
                task.TimeoutActionLane = null;
            }

            if (!IsTerminal(attempt.Status))
            {
                attempt.Status = TaskExecutionStatus.WaitingResponse;
                attempt.WaitingSinceUtc = sentOnUtc;
            }

            if (!IsTerminal(instance.Status))
            {
                instance.Status = OrchestrationInstanceStatus.Running;
                instance.WaitingSinceUtc = null;
                instance.IdleSinceUtc = null;
                instance.IdleExpiresOnUtc = null;
                instance.LastUpdatedOnUtc = sentOnUtc;
            }

            await PersistStateMutationAsync(async () =>
            {
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
                    TransitionType = "TaskDispatched",
                    FromStatus = previousDispatchStatus,
                    ToStatus = TaskExecutionStatus.WaitingResponse.ToString(),
                    OccurredOnUtc = dispatchedOnUtc,
                    Message = $"Task '{task.TaskKey}' command published with transport '{command.RemoteCommandTransport}'.",
                    Payload = string.IsNullOrWhiteSpace(command.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(command.Payload),
                    ProducedBy = nameof(RemoteCommandDispatchAction)
                }, cancellationToken);
            }, cancellationToken);

            await ScheduleResponseTimeoutAsync(instance, task, attempt, dispatch, cancellationToken);
            return true;
        }

        private async Task<DateTime?> ResolveResponseDeadlineAsync(
            OrchestrationInstance instance,
            TaskExecution task,
            DateTime sentOnUtc,
            CancellationToken cancellationToken)
        {
            if (_timeoutDeadlineResolver is null)
            {
                return null;
            }

            try
            {
                return await _timeoutDeadlineResolver.ResolveAsync(instance.Id, task.Id, sentOnUtc, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not resolve response timeout policy for task '{TaskId}'.", task.Id);
                return null;
            }
        }

        private async Task ScheduleResponseTimeoutAsync(
            OrchestrationInstance instance,
            TaskExecution task,
            TaskExecutionAttempt attempt,
            TaskDispatch dispatch,
            CancellationToken cancellationToken)
        {
            if (_timeoutScheduler is null || task.ResponseDeadlineUtc is not { } dueOnUtc)
            {
                return;
            }

            try
            {
                await _timeoutScheduler.ScheduleAsync(new TaskResponseTimeoutScheduleRequest
                {
                    OrchestrationInstanceId = instance.Id,
                    TaskExecutionId = task.Id,
                    TaskExecutionAttemptId = attempt.Id,
                    DispatchId = dispatch.Id,
                    AttemptNumber = attempt.AttemptNumber,
                    DueOnUtc = dueOnUtc
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // The waiting state is durable and the repair process will restore the missing action reference.
                // Retrying this already-published command would risk a duplicate transport publication.
                _logger.LogWarning(
                    exception,
                    "Task response timeout for dispatch '{DispatchId}' could not be scheduled after publication.",
                    dispatch.Id);
            }
        }

        private Task RecordIgnoredPublishedDispatchAsync(
            OrchestrationInstance instance,
            TaskExecution task,
            TaskExecutionAttempt attempt,
            TaskDispatch dispatch,
            RemoteCommand command,
            CancellationToken cancellationToken)
            => _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskDispatchPublishIgnored",
                FromStatus = dispatch.DispatchStatus,
                ToStatus = dispatch.DispatchStatus,
                OccurredOnUtc = DateTime.UtcNow,
                Message = $"Task '{task.TaskKey}' command publication completed after the dispatch was no longer executable.",
                Payload = string.IsNullOrWhiteSpace(command.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(command.Payload),
                ProducedBy = nameof(RemoteCommandDispatchAction)
            }, cancellationToken);

        private async Task ContinueFireAndForgetAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            if (command.AwaitResponse)
            {
                return;
            }

            var sagaEngine = _serviceProvider.GetService<ISagaEngine>();
            if (sagaEngine is null)
            {
                return;
            }

            var instance = await _instanceRepository.GetById(ParseId(command.OrchestrationInstanceId), cancellationToken);
            if (IsTerminal(instance.Status))
            {
                return;
            }

            await sagaEngine.OrchestrateAsync(new ForwardIntent
            {
                ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
                IngressTransport = IngressTransport.Messaging,
                MessageMetadata = new OrchestrationMessageMetadata
                {
                    SagaId = string.IsNullOrWhiteSpace(instance.SagaId) ? instance.Id.ToString() : instance.SagaId,
                    OrchestrationInstanceId = instance.Id.ToString(),
                    CurrentStage = command.StageKey,
                    CurrentTasks = string.IsNullOrWhiteSpace(command.TaskKey)
                        ? Array.Empty<string>()
                        : [command.TaskKey],
                    CorrelationId = instance.CorrelationId
                },
                PropagationMetadata = command.PropagationMetadata,
                Payload = instance.SnapshotPayload?.DeepClone()
            }, cancellationToken);
        }

        private async Task MarkDispatchFailedAsync(RemoteCommand command, Exception exception, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(ParseId(command.OrchestrationInstanceId), cancellationToken);
            var task = await _taskRepository.GetById(ParseId(command.TaskExecutionId), cancellationToken);
            var attempt = await _attemptRepository.GetById(ParseId(command.TaskExecutionAttemptId), cancellationToken);
            var dispatch = await _dispatchRepository.GetById(ParseId(command.DispatchId), cancellationToken);

            dispatch.DispatchStatus = "Failed";
            dispatch.FailedOnUtc = now;
            dispatch.FailureReason = exception.Message;
            task.Status = TaskExecutionStatus.Failed;
            task.FailedOnUtc = now;
            attempt.Status = TaskExecutionStatus.Failed;
            attempt.FailedOnUtc = now;
            attempt.ErrorCode = exception is RemoteCommandConfigurationException
                ? "RemoteCommandConfigurationError"
                : "CommandDispatchFailed";
            attempt.ErrorMessage = exception.Message;
            // A missing transport executor is a deterministic host configuration error.
            // It is the narrow dispatch failure that legitimately terminates the saga;
            // transient delivery/response failures remain Running and follow retry/idle policy.
            instance.Status = exception is RemoteCommandConfigurationException
                ? OrchestrationInstanceStatus.Failed
                : OrchestrationInstanceStatus.Running;
            instance.FailedOnUtc = instance.Status == OrchestrationInstanceStatus.Failed ? now : null;
            instance.ErrorSummary = exception.Message;
            instance.LastUpdatedOnUtc = now;

            await PersistStateMutationAsync(async () =>
            {
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
                    TransitionType = "TaskDispatchFailed",
                    FromStatus = "Enqueued",
                    ToStatus = TaskExecutionStatus.Failed.ToString(),
                    OccurredOnUtc = now,
                    Message = exception.Message,
                    ProducedBy = nameof(RemoteCommandDispatchAction)
                }, cancellationToken);
            }, cancellationToken);
        }

        private async Task<bool> MarkDispatchRetryPendingAsync(RemoteCommand command, Exception exception, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(ParseId(command.OrchestrationInstanceId), cancellationToken);
            var task = await _taskRepository.GetById(ParseId(command.TaskExecutionId), cancellationToken);
            var attempt = await _attemptRepository.GetById(ParseId(command.TaskExecutionAttemptId), cancellationToken);
            var dispatch = await _dispatchRepository.GetById(ParseId(command.DispatchId), cancellationToken);
            if (IsFinished(dispatch.DispatchStatus))
            {
                await _transitionRepository.Create(new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    StageExecutionId = task.StageExecutionId,
                    TaskExecutionId = task.Id,
                    TaskExecutionAttemptId = attempt.Id,
                    TransitionType = "TaskDispatchFailureIgnored",
                    FromStatus = dispatch.DispatchStatus,
                    ToStatus = dispatch.DispatchStatus,
                    OccurredOnUtc = now,
                    Message = $"Dispatch failure ignored because task '{task.TaskKey}' already has a terminal callback state: {exception.Message}",
                    ProducedBy = nameof(RemoteCommandDispatchAction)
                }, cancellationToken);
                return false;
            }

            var previousDispatchStatus = dispatch.DispatchStatus;
            dispatch.DispatchStatus = "RetryPending";
            dispatch.FailedOnUtc = now;
            dispatch.FailureReason = exception.Message;

            if (!IsTerminal(task.Status))
            {
                task.Status = TaskExecutionStatus.Running;
                task.WaitingSinceUtc = null;
            }

            if (!IsTerminal(attempt.Status))
            {
                attempt.Status = TaskExecutionStatus.Running;
                attempt.WaitingSinceUtc = null;
            }

            if (!IsTerminal(instance.Status))
            {
                instance.Status = OrchestrationInstanceStatus.Running;
                instance.WaitingSinceUtc = null;
                instance.LastUpdatedOnUtc = now;
            }

            await PersistStateMutationAsync(async () =>
            {
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
                    TransitionType = "TaskDispatchRetryPending",
                    FromStatus = previousDispatchStatus,
                    ToStatus = dispatch.DispatchStatus,
                    OccurredOnUtc = now,
                    Message = $"Task '{task.TaskKey}' command dispatch failed and will be retried by Mule: {exception.Message}",
                    ProducedBy = nameof(RemoteCommandDispatchAction)
                }, cancellationToken);
            }, cancellationToken);
            return true;
        }

        private async Task PersistStateMutationAsync(Func<Task> mutation, CancellationToken cancellationToken)
        {
            if (_storageUnitOfWork is null)
            {
                await mutation();
                return;
            }

            await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
            using (_storageUnitOfWork.DeferAutoSave())
            {
                await mutation();
                await _storageUnitOfWork.SaveChanges(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        private Id ParseId(string value)
            => new(System.Ulid.Parse(value));

        private bool HasRuntimeDispatchState(RemoteCommand command)
            => !string.IsNullOrWhiteSpace(command.OrchestrationInstanceId)
                && !string.IsNullOrWhiteSpace(command.TaskExecutionId)
                && !string.IsNullOrWhiteSpace(command.TaskExecutionAttemptId)
                && !string.IsNullOrWhiteSpace(command.DispatchId);

        private static bool IsFinished(string status)
            => string.Equals(status, "Acknowledged", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "TimedOut", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);

        private static bool IsCancelled(string status)
            => string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);

        private static bool IsDispatchPending(string status)
            => string.Equals(status, "QueuePending", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Enqueued", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "RetryPending", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "ReplayPending", StringComparison.OrdinalIgnoreCase);

        private static bool IsTerminal(TaskExecutionStatus status)
            => status is TaskExecutionStatus.Completed
                or TaskExecutionStatus.CompletedWithErrors
                or TaskExecutionStatus.Failed
                or TaskExecutionStatus.TimedOut
                or TaskExecutionStatus.Cancelled
                or TaskExecutionStatus.Compensated;

        private static bool IsTerminal(OrchestrationInstanceStatus status)
            => status is OrchestrationInstanceStatus.Completed
                or OrchestrationInstanceStatus.CompletedWithErrors
                or OrchestrationInstanceStatus.Failed
                or OrchestrationInstanceStatus.DeadLettered
                or OrchestrationInstanceStatus.Aborted
                or OrchestrationInstanceStatus.Compensating
                or OrchestrationInstanceStatus.Compensated
                or OrchestrationInstanceStatus.Stopped;
    }
}
