namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using System.Text.Json.Nodes;

internal sealed class RecordingRemoteCommandDispatcher : IRemoteCommandDispatcher
{
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly Queue<Exception> _plannedFailures = new();
    private readonly List<RemoteCommand> _commands = new();

    public RecordingRemoteCommandDispatcher(
        IOrchestrationInstanceRepository instanceRepository,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
    }

    public IReadOnlyList<RemoteCommand> Commands => _commands;

    public void FailNext(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _plannedFailures.Enqueue(exception);
    }

    public async Task DispatchAsync(RemoteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _commands.Add(Clone(command));
        if (_plannedFailures.Count > 0)
        {
            throw _plannedFailures.Dequeue();
        }

        if (!HasRuntimeDispatchState(command))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var instance = await _instanceRepository.GetById(ParseId(command.OrchestrationInstanceId), cancellationToken);
        var task = await _taskRepository.GetById(ParseId(command.TaskExecutionId), cancellationToken);
        var attempt = await _attemptRepository.GetById(ParseId(command.TaskExecutionAttemptId), cancellationToken);
        var dispatch = await _dispatchRepository.GetById(ParseId(command.DispatchId), cancellationToken);

        dispatch.SentOnUtc = now;
        dispatch.FailedOnUtc = null;
        dispatch.FailureReason = null;

        if (command.AwaitResponse)
        {
            dispatch.DispatchStatus = "WaitingResponse";
            task.Status = TaskExecutionStatus.WaitingResponse;
            task.WaitingSinceUtc = now;
            attempt.Status = TaskExecutionStatus.WaitingResponse;
            attempt.WaitingSinceUtc = now;
            instance.Status = OrchestrationInstanceStatus.Waiting;
            instance.WaitingSinceUtc = now;
        }
        else
        {
            dispatch.DispatchStatus = "Completed";
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
    }

    private static RemoteCommand Clone(RemoteCommand command)
        => new()
        {
            RemoteCommandTransport = command.RemoteCommandTransport,
            Payload = command.Payload,
            SettingsPayload = command.SettingsPayload,
            OrchestrationInstanceId = command.OrchestrationInstanceId,
            StageExecutionId = command.StageExecutionId,
            TaskExecutionId = command.TaskExecutionId,
            TaskExecutionAttemptId = command.TaskExecutionAttemptId,
            DispatchId = command.DispatchId,
            StageKey = command.StageKey,
            TaskKey = command.TaskKey,
            AwaitResponse = command.AwaitResponse,
            MessageMetadata = Clone(command.MessageMetadata)!
        };

    private static OrchestrationMessageMetadata? Clone(OrchestrationMessageMetadata? metadata)
        => metadata is null
            ? null
            : new OrchestrationMessageMetadata
            {
                SagaId = metadata.SagaId,
                OrchestrationInstanceId = metadata.OrchestrationInstanceId,
                CurrentStage = metadata.CurrentStage,
                CurrentTasks = metadata.CurrentTasks?.ToArray(),
                CorrelationId = metadata.CorrelationId,
                TaskExecutionId = metadata.TaskExecutionId,
                DispatchId = metadata.DispatchId,
                Attempt = metadata.Attempt,
                ReplyAddress = Clone(metadata.ReplyAddress)
            };

    private static OrchestrationReplyAddress? Clone(OrchestrationReplyAddress? replyAddress)
        => replyAddress is null
            ? null
            : new OrchestrationReplyAddress
            {
                Transport = replyAddress.Transport,
                SettingsPayload = replyAddress.SettingsPayload,
                Metadata = replyAddress.Metadata.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value?.DeepClone(),
                    StringComparer.Ordinal)
            };

    private static bool HasRuntimeDispatchState(RemoteCommand command)
        => !string.IsNullOrWhiteSpace(command.OrchestrationInstanceId)
            && !string.IsNullOrWhiteSpace(command.TaskExecutionId)
            && !string.IsNullOrWhiteSpace(command.TaskExecutionAttemptId)
            && !string.IsNullOrWhiteSpace(command.DispatchId);

    private static Id ParseId(string value)
        => new(Ulid.Parse(value));
}
