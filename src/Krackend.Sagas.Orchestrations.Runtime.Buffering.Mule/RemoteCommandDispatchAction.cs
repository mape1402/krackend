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
using System.Diagnostics;

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
                await MuleCooperativeExecution.RunAsync(context, () => ExecuteCooperativelyAsync(
                    context,
                    command,
                    markRetryPending: true,
                    deferOnContention: true,
                    recordPublicationReceipt: true,
                    cancellationToken));
                return;
            }

            await ExecuteCoreAsync(context, command, cancellationToken);
        }

        /// <summary>
        /// Executes the realtime publication path without first materializing and reclaiming
        /// a Mule action. The already-persisted task dispatch is the crash-recovery intent;
        /// the caller materializes a Mule action only when this attempt fails.
        /// </summary>
        internal async ValueTask ExecuteDirectAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (_admissionController is not null)
            {
                await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.Dispatch, cancellationToken);
            }

            var directAction = new DurableAction
            {
                Id = global::Mule.Ulid.NewUlid(),
                Key = MuleActionKeys.RemoteCommandDispatchActionKey,
                Lane = MuleActionKeys.DispatchLane,
                CorrelationId = command.OrchestrationInstanceId,
                DeduplicationKey = MuleRemoteCommandDispatcher.BuildDeduplicationKey(command),
                Status = DurableActionStatus.Pending,
                CreatedOnUtc = DateTimeOffset.UtcNow
            };
            var context = new MuleActionContext<RemoteCommand>(directAction, _serviceProvider, command);

            if (command.IsPreparedDirectActivation && command.AwaitResponse && HasRuntimeDispatchState(command))
            {
                await ExecutePreparedAwaitedDirectAsync(context, command, cancellationToken);
                return;
            }

            if (HasRuntimeDispatchState(command) && _coordinator is not null)
            {
                await ExecuteCooperativelyAsync(
                    context,
                    command,
                    markRetryPending: false,
                    deferOnContention: false,
                    recordPublicationReceipt: false,
                    cancellationToken);
                return;
            }

            await ExecuteCoreAsync(context, command, cancellationToken);
        }

        private async Task ExecutePreparedAwaitedDirectAsync(
            MuleActionContext<RemoteCommand> context,
            RemoteCommand command,
            CancellationToken cancellationToken)
        {
            var published = false;
            try
            {
                var executor = _serviceProvider.GetKeyedService<IRemoteCommandExecutor>(command.RemoteCommandTransport)
                    ?? throw new RemoteCommandConfigurationException(
                        $"No remote command executor is configured for '{command.RemoteCommandTransport}'.");
                _messageMetadataSetter.Set(command.MessageMetadata ?? new OrchestrationMessageMetadata());
                _propagationMetadataSetter?.Set(command.PropagationMetadata ?? new OrchestrationPropagationMetadata());

                var publicationStarted = Stopwatch.GetTimestamp();
                try { await executor.ExecuteAsync(command, cancellationToken); }
                finally
                {
                    RuntimeConcurrencyMetrics.PublicationMilliseconds.Record(
                        Stopwatch.GetElapsedTime(publicationStarted).TotalMilliseconds);
                }
                published = true;
                if (command.PreparedResponseDeadlineUtc is { } responseDeadlineUtc &&
                    _timeoutScheduler is not null)
                {
                    // Prepared dispatches already carry every identity and the exact
                    // deadline committed by the engine. Schedule the durable timer
                    // directly after the transport confirms publication; no state
                    // polling or graph reload is required on the healthy path.
                    await ScheduleResponseTimeoutAsync(
                        _timeoutScheduler,
                        new TaskResponseTimeoutScheduleRequest
                        {
                            OrchestrationInstanceId = ParseId(command.OrchestrationInstanceId),
                            TaskExecutionId = ParseId(command.TaskExecutionId),
                            TaskExecutionAttemptId = ParseId(command.TaskExecutionAttemptId),
                            DispatchId = ParseId(command.DispatchId),
                            AttemptNumber = command.MessageMetadata?.Attempt ?? 0,
                            DueOnUtc = responseDeadlineUtc
                        },
                        ParseId(command.DispatchId),
                        cancellationToken);
                }
                // The intent transaction already persisted WaitingResponse and the
                // response deadline. That state is the durable timeout source used by
                // the deadline scanner, so the healthy path must not update the task a
                // second time merely to acknowledge transport publication. If the
                // process dies in the publish ambiguity window, normal dispatch recovery
                // may publish again (at-least-once) and the command identity deduplicates
                // it. Exceptional post-publication failures still record a receipt in
                // the catch block below before recovery is materialized.
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (RemoteCommandConfigurationException exception)
            {
                if (_coordinator is not null)
                    await _coordinator.TryExecuteAsync(
                        ParseId(command.OrchestrationInstanceId),
                        token => MarkDispatchFailedAsync(command, exception, token),
                        cancellationToken,
                        waitForLease: true);
                else
                    await MarkDispatchFailedAsync(command, exception, cancellationToken);
                _terminalFailureMarker.MarkTerminal(context);
                throw;
            }
            catch
            {
                if (published && _dispatchRepository is ITaskDispatchPublicationReceiptRepository receipts)
                {
                    // The transport returned successfully. Preserve that fact before the
                    // dispatcher materializes recovery so a retry can only confirm state,
                    // never publish the command twice.
                    await receipts.RecordPublicationReceipt(
                        ParseId(command.DispatchId), DateTime.UtcNow, cancellationToken);
                }
                throw;
            }
        }

        private async Task ExecuteCooperativelyAsync(
            MuleActionContext<RemoteCommand> context,
            RemoteCommand command,
            bool markRetryPending,
            bool deferOnContention,
            bool recordPublicationReceipt,
            CancellationToken cancellationToken)
        {
            var instanceId = ParseId(command.OrchestrationInstanceId);
            // The publication receipt is atomic and belongs to the independent dispatch
            // graph. Do not reserve the orchestration lease while performing read-only
            // validation, metadata capture, or transport I/O. A very fast callback can be
            // durably accepted and deferred until the single confirmation mutation below.
            var currentState = await GetCurrentDispatchStateAsync(command, cancellationToken);
            if (currentState is null) return;
            var receipts = _dispatchRepository as ITaskDispatchPublicationReceiptRepository
                ?? throw new RemoteCommandConfigurationException(
                    "The task dispatch repository must support atomic publication receipts.");
            DateTime? publishedOnUtc = currentState.Dispatch.SentOnUtc;

            try
            {
                if (publishedOnUtc is null)
                {
                    // The receipt is a conditional, single-column write. Requiring
                    // this capability avoids losing concurrent callback state.
                    var executor = _serviceProvider.GetKeyedService<IRemoteCommandExecutor>(command.RemoteCommandTransport)
                        ?? throw new RemoteCommandConfigurationException($"No remote command executor is configured for '{command.RemoteCommandTransport}'.");
                    _messageMetadataSetter.Set(command.MessageMetadata ?? new OrchestrationMessageMetadata());
                    _propagationMetadataSetter?.Set(command.PropagationMetadata ?? new OrchestrationPropagationMetadata());
                    var publicationStarted = Stopwatch.GetTimestamp();
                    try { await executor.ExecuteAsync(command, cancellationToken); }
                    finally { RuntimeConcurrencyMetrics.PublicationMilliseconds.Record(Stopwatch.GetElapsedTime(publicationStarted).TotalMilliseconds); }
                    publishedOnUtc = DateTime.UtcNow;
                    if (recordPublicationReceipt || !command.AwaitResponse)
                    {
                        await receipts.RecordPublicationReceipt(
                            ParseId(command.DispatchId), publishedOnUtc.Value, cancellationToken);
                    }
                }

                if (command.AwaitResponse)
                {
                    bool? fastConfirmation;
                    try
                    {
                        fastConfirmation = await TryConfirmAwaitedPublicationWithoutInstanceLeaseAsync(
                            command, currentState, publishedOnUtc.Value, cancellationToken);
                    }
                    catch when (!recordPublicationReceipt)
                    {
                        // The transport accepted the command but healthy confirmation
                        // failed. Persist the small publication receipt only on this
                        // exceptional path so recovery never repeats a known send.
                        await receipts.RecordPublicationReceipt(
                            ParseId(command.DispatchId), publishedOnUtc.Value, cancellationToken);
                        throw;
                    }
                    if (fastConfirmation.HasValue) return;
                    if (!recordPublicationReceipt)
                    {
                        await receipts.RecordPublicationReceipt(
                            ParseId(command.DispatchId), publishedOnUtc.Value, cancellationToken);
                    }
                }

                var acquired = await _coordinator.TryExecuteAsync(instanceId, async token =>
                {
                    if (await MarkDispatchPublishedAsync(command, publishedOnUtc.Value, token))
                        await ContinueFireAndForgetAsync(command, token);
                }, cancellationToken, waitForLease: true);
                if (!acquired)
                {
                    if (deferOnContention)
                        MuleCooperativeExecution.Defer(context, "krackend.publication-confirmation-pending");
                    else
                        throw new OrchestrationInstanceLeaseUnavailableException(instanceId);
                }
            }
            catch (RuntimeStateConflictException) { throw; }
            catch (RemoteCommandConfigurationException exception)
            {
                await _coordinator.TryExecuteAsync(instanceId, token => MarkDispatchFailedAsync(command, exception, token),
                    cancellationToken, waitForLease: true);
                _terminalFailureMarker.MarkTerminal(context);
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // A real transport/storage failure keeps its functional retry budget.
                // If publication succeeded, its durable receipt suppresses a repeat send.
                if (markRetryPending)
                {
                    await _coordinator.TryExecuteAsync(instanceId, async token =>
                    {
                        await MarkDispatchRetryPendingAsync(command, exception, token);
                    }, cancellationToken, waitForLease: true);
                }
                throw;
            }
        }

        private async Task<bool?> TryConfirmAwaitedPublicationWithoutInstanceLeaseAsync(
            RemoteCommand command,
            TaskDispatchExecutionState state,
            DateTime sentOnUtc,
            CancellationToken cancellationToken)
        {
            var responseDeadlineUtc = state.Task.ResponseDeadlineUtc ??
                await ResolveResponseDeadlineAsync(state.Instance, state.Task, sentOnUtc, cancellationToken);
            var confirmed = false;

            if (state.Task.Status == TaskExecutionStatus.WaitingResponse &&
                state.Attempt.Status == TaskExecutionStatus.WaitingResponse &&
                _dispatchRepository is ITaskDispatchPublicationStateRepository atomicPublication)
            {
                // Immediate awaited tasks were armed in the dispatch-intent commit.
                // One conditional row update is the whole healthy publication path.
                confirmed = await atomicPublication.TryConfirmPublication(
                    state.Dispatch.Id,
                    "WaitingResponse",
                    sentOnUtc,
                    cancellationToken);
            }
            else
            {
                if (_dispatchRepository is not ITaskDispatchAwaitedPublicationRepository atomicGraph ||
                    _storageUnitOfWork is null)
                    return null;

                await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
                using (_storageUnitOfWork.DeferAutoSave())
                {
                    confirmed = await atomicGraph.TryConfirmAwaitedPublication(
                        state.Dispatch.Id,
                        state.Task.Id,
                        state.Attempt.Id,
                        sentOnUtc,
                        responseDeadlineUtc,
                        cancellationToken);
                    await _storageUnitOfWork.SaveChanges(cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
            }

            if (confirmed && responseDeadlineUtc is { } dueOnUtc && _timeoutScheduler is not null)
            {
                await ScheduleResponseTimeoutAsync(
                    _timeoutScheduler,
                    new TaskResponseTimeoutScheduleRequest
                    {
                        OrchestrationInstanceId = state.Instance.Id,
                        TaskExecutionId = state.Task.Id,
                        TaskExecutionAttemptId = state.Attempt.Id,
                        DispatchId = state.Dispatch.Id,
                        AttemptNumber = state.Attempt.AttemptNumber,
                        DueOnUtc = dueOnUtc
                    },
                    state.Dispatch.Id,
                    cancellationToken);
            }

            return confirmed;
        }

        private async Task ExecuteCoreAsync(
            MuleActionContext<RemoteCommand> context,
            RemoteCommand command,
            CancellationToken cancellationToken)
        {
            try
            {
                if (HasRuntimeDispatchState(command) &&
                    await GetCurrentDispatchStateAsync(command, cancellationToken) is null)
                {
                    return;
                }

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

        private async Task<TaskDispatchExecutionState?> GetCurrentDispatchStateAsync(
            RemoteCommand command,
            CancellationToken cancellationToken)
        {
            if (!HasRuntimeDispatchState(command)) return null;

            var instanceId = ParseId(command.OrchestrationInstanceId);
            var taskId = ParseId(command.TaskExecutionId);
            var attemptId = ParseId(command.TaskExecutionAttemptId);
            var dispatchId = ParseId(command.DispatchId);
            var state = await ReadExecutionStateAsync(dispatchId, cancellationToken);
            var instance = state.Instance;
            var task = state.Task;
            var attempt = state.Attempt;
            var dispatch = state.Dispatch;

            if (instance.Id == instanceId &&
                task.OrchestrationInstanceId == instanceId &&
                attempt.TaskExecutionId == taskId &&
                dispatch.TaskExecutionAttemptId == attemptId &&
                !IsTerminal(instance.Status) &&
                IsPublicationCandidate(task.Status, attempt.Status) &&
                (task.LastAttemptNumber == 0 || task.LastAttemptNumber == attempt.AttemptNumber) &&
                IsDispatchPending(dispatch.DispatchStatus))
            {
                return state;
            }

            _logger.LogDebug(
                "Suppressed stale durable dispatch '{DispatchId}' for saga '{SagaId}'.",
                dispatch.Id,
                instance.SagaId);
            return null;
        }

        private async Task<TaskDispatchExecutionState> ReadExecutionStateAsync(
            Id dispatchId,
            CancellationToken cancellationToken)
        {
            if (_dispatchRepository is ITaskDispatchExecutionStateReader stateReader)
            {
                return await stateReader.GetExecutionState(dispatchId, cancellationToken);
            }

            var dispatch = await _dispatchRepository.GetById(dispatchId, cancellationToken);
            var attempt = await _attemptRepository.GetById(dispatch.TaskExecutionAttemptId, cancellationToken);
            var task = await _taskRepository.GetById(attempt.TaskExecutionId, cancellationToken);
            var instance = await _instanceRepository.GetById(task.OrchestrationInstanceId, cancellationToken);
            return new TaskDispatchExecutionState(instance, task, attempt, dispatch);
        }

        private static void MergeCommandMetadata(TaskDispatch dispatch, RemoteCommand command)
        {
            var metadata = RemoteCommandMetadataBuilder.Build(command.MessageMetadata, command.PropagationMetadata);
            foreach (var existing in dispatch.Metadata)
            {
                if (!metadata.ContainsKey(existing.Key))
                {
                    metadata[existing.Key] = existing.Value?.DeepClone();
                }
            }

            dispatch.Metadata = metadata;
        }

        private async Task<bool> MarkDispatchPublishedAsync(
            RemoteCommand command,
            DateTime dispatchedOnUtc,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            if (command.AwaitResponse)
            {
                return await MarkAwaitedDispatchPublishedAsync(command, dispatchedOnUtc, dispatchedOnUtc, cancellationToken);
            }

            var state = await ReadExecutionStateAsync(ParseId(command.DispatchId), cancellationToken);
            var instance = state.Instance;
            var task = state.Task;
            var attempt = state.Attempt;
            var dispatch = state.Dispatch;

            if (IsFinished(dispatch.DispatchStatus))
            {
                await RecordIgnoredPublishedDispatchAsync(instance, task, attempt, dispatch, command, cancellationToken);
                return false;
            }

            var previousDispatchStatus = dispatch.DispatchStatus;
            MergeCommandMetadata(dispatch, command);
            dispatch.SentOnUtc ??= dispatchedOnUtc;
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
                instance.RecordCompletedTasks(1);
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
                await ConfirmDispatchPublicationAsync(dispatch, dispatchedOnUtc, cancellationToken);
                await _attemptRepository.Update(attempt, cancellationToken);
                await _taskRepository.Update(task, cancellationToken);
                await _instanceRepository.Update(instance, cancellationToken);
                await _transitionRepository.Create(new ExecutionTransition
                {
                    Id = Id.NewDistributed(),
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
            var state = await ReadExecutionStateAsync(dispatchId, cancellationToken);
            var instance = state.Instance;
            var task = state.Task;
            var attempt = state.Attempt;
            var dispatch = state.Dispatch;
            if (IsFinished(dispatch.DispatchStatus))
            {
                await RecordIgnoredPublishedDispatchAsync(instance, task, attempt, dispatch, command, cancellationToken);
                return false;
            }

            var previousDispatchStatus = dispatch.DispatchStatus;
            MergeCommandMetadata(dispatch, command);
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
                await ConfirmDispatchPublicationAsync(dispatch, sentOnUtc, cancellationToken);
                await _attemptRepository.Update(attempt, cancellationToken);
                await _taskRepository.Update(task, cancellationToken);
                await _instanceRepository.Update(instance, cancellationToken);
                await _transitionRepository.Create(new ExecutionTransition
                {
                    Id = Id.NewDistributed(),
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

            var timeoutRequest = task.ResponseDeadlineUtc is { } dueOnUtc
                ? new TaskResponseTimeoutScheduleRequest
                {
                    OrchestrationInstanceId = instance.Id,
                    TaskExecutionId = task.Id,
                    TaskExecutionAttemptId = attempt.Id,
                    DispatchId = dispatch.Id,
                    AttemptNumber = attempt.AttemptNumber,
                    DueOnUtc = dueOnUtc
                }
                : null;

            if (timeoutRequest is not null && _coordinator is IOrchestrationPostLeaseWork postLeaseWork)
            {
                // Timer creation is durable housekeeping. Keeping it inside the instance
                // lease makes a fast callback wait behind its own dispatch.
                await postLeaseWork.ExecuteIsolatedAfterLeaseReleaseAsync(
                    instance.Id,
                    async (services, token) =>
                    {
                        var scheduler = services.GetService<ITaskResponseTimeoutScheduler>();
                        if (scheduler is not null)
                            await ScheduleResponseTimeoutAsync(scheduler, timeoutRequest, dispatch.Id, token);
                    },
                    cancellationToken);
            }
            else if (timeoutRequest is not null && _timeoutScheduler is not null)
            {
                await ScheduleResponseTimeoutAsync(
                    _timeoutScheduler, timeoutRequest, dispatch.Id, cancellationToken);
            }
            return true;
        }

        private async Task ConfirmDispatchPublicationAsync(
            TaskDispatch dispatch,
            DateTime publishedOnUtc,
            CancellationToken cancellationToken)
        {
            if (_dispatchRepository is ITaskDispatchPublicationStateRepository atomicPublication)
            {
                if (!await atomicPublication.TryConfirmPublication(
                        dispatch.Id,
                        dispatch.DispatchStatus,
                        publishedOnUtc,
                        cancellationToken))
                    throw new RuntimeStateConflictException(
                        $"Task dispatch '{dispatch.Id}' changed before publication confirmation.");
                return;
            }

            await _dispatchRepository.Update(dispatch, cancellationToken);
        }

        private async Task<DateTime?> ResolveResponseDeadlineAsync(
            OrchestrationInstance instance,
            TaskExecution task,
            DateTime sentOnUtc,
            CancellationToken cancellationToken)
        {
            // TaskAttemptDispatcher persists the resolved timeout policy on the task.
            // Use that immutable snapshot on the hot path instead of re-reading the
            // instance, task, stage and artifact after every publication.
            if (task.Metadata is not null && task.Metadata.TryGetValue("Timeout", out var timeoutNode))
            {
                var configured = timeoutNode?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(configured)) return null;
                if (TimeSpan.TryParse(configured, System.Globalization.CultureInfo.InvariantCulture, out var timeout))
                    return sentOnUtc.Add(timeout);
            }

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
            ITaskResponseTimeoutScheduler scheduler,
            TaskResponseTimeoutScheduleRequest request,
            Id dispatchId,
            CancellationToken cancellationToken)
        {
            if (!scheduler.UsesDurableScheduling)
            {
                return;
            }

            try
            {
                await scheduler.ScheduleAsync(request, cancellationToken);
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
                    dispatchId);
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
                Id = Id.NewDistributed(),
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
            var state = await ReadExecutionStateAsync(ParseId(command.DispatchId), cancellationToken);
            var instance = state.Instance;
            var task = state.Task;
            var attempt = state.Attempt;
            var dispatch = state.Dispatch;

            MergeCommandMetadata(dispatch, command);
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
                    Id = Id.NewDistributed(),
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
            var state = await ReadExecutionStateAsync(ParseId(command.DispatchId), cancellationToken);
            var instance = state.Instance;
            var task = state.Task;
            var attempt = state.Attempt;
            var dispatch = state.Dispatch;
            if (IsFinished(dispatch.DispatchStatus))
            {
                await _transitionRepository.Create(new ExecutionTransition
                {
                    Id = Id.NewDistributed(),
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
            MergeCommandMetadata(dispatch, command);
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
                    Id = Id.NewDistributed(),
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

        private static bool IsPublicationCandidate(
            TaskExecutionStatus taskStatus,
            TaskExecutionStatus attemptStatus)
            => taskStatus == TaskExecutionStatus.Running && attemptStatus == TaskExecutionStatus.Running ||
               taskStatus == TaskExecutionStatus.WaitingResponse && attemptStatus == TaskExecutionStatus.WaitingResponse;

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
