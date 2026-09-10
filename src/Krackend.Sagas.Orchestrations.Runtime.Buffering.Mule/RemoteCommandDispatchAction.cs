using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly ITaskExecutionAttemptRepository _attemptRepository;
        private readonly ITaskDispatchRepository _dispatchRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IMuleTerminalFailureMarker _terminalFailureMarker;

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
            IMuleTerminalFailureMarker terminalFailureMarker)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _messageMetadataSetter = messageMetadataSetter ?? throw new ArgumentNullException(nameof(messageMetadataSetter));
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
            _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _terminalFailureMarker = terminalFailureMarker ?? throw new ArgumentNullException(nameof(terminalFailureMarker));
        }

        /// <summary>
        /// Executes the queued remote command and updates runtime dispatch state.
        /// </summary>
        public async ValueTask ExecuteAsync(MuleActionContext<RemoteCommand> context, CancellationToken cancellationToken)
        {
            var command = context.Payload ?? throw new InvalidOperationException("Remote command payload cannot be empty.");

            try
            {
                var executor = _serviceProvider.GetKeyedService<IRemoteCommandExecutor>(command.RemoteCommandTransport)
                    ?? throw new RemoteCommandConfigurationException($"No remote command executor is configured for '{command.RemoteCommandTransport}'.");

                _messageMetadataSetter.Set(command.MessageMetadata ?? new OrchestrationMessageMetadata());
                await executor.ExecuteAsync(command, cancellationToken);
                if (HasRuntimeDispatchState(command))
                {
                    await MarkDispatchPublishedAsync(command, cancellationToken);
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
                if (HasRuntimeDispatchState(command))
                {
                    await MarkDispatchFailedAsync(command, exception, cancellationToken);
                }

                throw;
            }
        }

        private async Task MarkDispatchPublishedAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(ParseId(command.OrchestrationInstanceId), cancellationToken);
            var task = await _taskRepository.GetById(ParseId(command.TaskExecutionId), cancellationToken);
            var attempt = await _attemptRepository.GetById(ParseId(command.TaskExecutionAttemptId), cancellationToken);
            var dispatch = await _dispatchRepository.GetById(ParseId(command.DispatchId), cancellationToken);

            dispatch.SentOnUtc = now;
            dispatch.DispatchStatus = command.AwaitResponse ? "WaitingResponse" : "Completed";
            dispatch.FailedOnUtc = null;
            dispatch.FailureReason = null;

            if (command.AwaitResponse)
            {
                task.Status = TaskExecutionStatus.WaitingResponse;
                task.WaitingSinceUtc = now;
                attempt.Status = TaskExecutionStatus.WaitingResponse;
                attempt.WaitingSinceUtc = now;
                instance.Status = OrchestrationInstanceStatus.Waiting;
                instance.WaitingSinceUtc = now;
            }
            else
            {
                task.Status = TaskExecutionStatus.Completed;
                task.CompletedOnUtc = now;
                attempt.Status = TaskExecutionStatus.Completed;
                attempt.CompletedOnUtc = now;
                instance.Status = OrchestrationInstanceStatus.Running;
                instance.WaitingSinceUtc = null;
            }

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
                TransitionType = "TaskDispatched",
                FromStatus = "Enqueued",
                ToStatus = task.Status.ToString(),
                OccurredOnUtc = now,
                Message = $"Task '{task.TaskKey}' command published with transport '{command.RemoteCommandTransport}'.",
                Payload = string.IsNullOrWhiteSpace(command.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(command.Payload),
                ProducedBy = nameof(RemoteCommandDispatchAction)
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
            instance.Status = task.OnErrorPolicy == OnErrorPolicy.Continue
                ? OrchestrationInstanceStatus.Running
                : OrchestrationInstanceStatus.Failed;
            instance.FailedOnUtc = task.OnErrorPolicy == OnErrorPolicy.Continue ? null : now;
            instance.ErrorSummary = exception.Message;
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
                TransitionType = "TaskDispatchFailed",
                FromStatus = "Enqueued",
                ToStatus = TaskExecutionStatus.Failed.ToString(),
                OccurredOnUtc = now,
                Message = exception.Message,
                ProducedBy = nameof(RemoteCommandDispatchAction)
            }, cancellationToken);
        }

        private Id ParseId(string value)
            => new(Ulid.Parse(value));

        private bool HasRuntimeDispatchState(RemoteCommand command)
            => !string.IsNullOrWhiteSpace(command.OrchestrationInstanceId)
                && !string.IsNullOrWhiteSpace(command.TaskExecutionId)
                && !string.IsNullOrWhiteSpace(command.TaskExecutionAttemptId)
                && !string.IsNullOrWhiteSpace(command.DispatchId);
    }
}
