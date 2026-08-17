namespace Krackend.Sagas.Orchestrations.Engine.DurableWork;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Mule;

/// <summary>
/// Mule action that publishes a canonical runtime dispatch through the configured transport adapter.
/// </summary>
[MuleAction("krackend.runtime.dispatch-task")]
public sealed class DispatchRuntimeTaskAction : IMuleAction<RuntimeDispatchEnvelope>
{
    private readonly IMessagingCommandDispatcher _messagingDispatcher;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly ICompensationExecutionRepository _compensationRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchRuntimeTaskAction"/> class.
    /// </summary>
    public DispatchRuntimeTaskAction(
        IMessagingCommandDispatcher messagingDispatcher,
        ITaskDispatchRepository dispatchRepository,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        IStageExecutionRepository stageRepository,
        ICompensationExecutionRepository compensationRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeReactiveEventPublisher reactiveEventPublisher)
    {
        _messagingDispatcher = messagingDispatcher ?? throw new ArgumentNullException(nameof(messagingDispatcher));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
        _compensationRepository = compensationRepository ?? throw new ArgumentNullException(nameof(compensationRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _reactiveEventPublisher = reactiveEventPublisher ?? throw new ArgumentNullException(nameof(reactiveEventPublisher));
    }

    /// <inheritdoc />
    public async ValueTask ExecuteAsync(MuleActionContext<RuntimeDispatchEnvelope> context, CancellationToken cancellationToken)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var envelope = context.Payload;

        if (envelope.Destination?.Kind != RuntimeTransportKind.Message)
        {
            await MarkUnsupportedTransportFailed(envelope, cancellationToken);
            return;
        }

        var result = await _messagingDispatcher.Dispatch(CreateCommand(envelope), cancellationToken);
        if (!result.Succeeded)
        {
            await MarkDispatchFailed(envelope, result, cancellationToken);
            throw new InvalidOperationException(result.FailureReason ?? $"Runtime dispatch failed with status '{result.Status}'.");
        }

        await MarkDispatchSent(envelope, result, cancellationToken);
    }

    private async Task MarkUnsupportedTransportFailed(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken)
    {
        var reason = $"Runtime dispatch transport '{envelope.Destination?.Kind}' is not supported yet.";
        var result = new MessagingDispatchResult
        {
            Succeeded = false,
            Status = "Failed",
            FailureReason = reason
        };

        await MarkDispatchFailed(envelope, result, cancellationToken);
        if (await TryGetCompensation(envelope, cancellationToken) is not null)
            return;

        var dispatch = await TryGetDispatch(envelope, cancellationToken);
        var instance = await TryGetInstance(envelope, cancellationToken);
        var taskExecution = await TryGetTaskExecution(envelope, cancellationToken);
        if (dispatch is null || instance is null || taskExecution is null)
            return;

        var failedOnUtc = DateTime.UtcNow;
        var attempt = await TryGetAttempt(taskExecution, dispatch, cancellationToken);
        if (attempt is not null)
        {
            attempt.Status = TaskExecutionStatus.Failed;
            attempt.FailedOnUtc = failedOnUtc;
            attempt.WaitingSinceUtc = null;
            attempt.ErrorCode = "UnsupportedDispatchTransport";
            attempt.ErrorMessage = reason;
            attempt.DispatchId = dispatch.Id;
            await _attemptRepository.Update(attempt, cancellationToken);
        }

        taskExecution.Status = TaskExecutionStatus.Failed;
        taskExecution.FailedOnUtc = failedOnUtc;
        taskExecution.WaitingSinceUtc = null;
        taskExecution.Metadata["failureReason"] = reason;
        taskExecution.Metadata["errorCode"] = "UnsupportedDispatchTransport";
        await _taskRepository.Update(taskExecution, cancellationToken);

        var stageExecution = await TryGetStageExecution(taskExecution, cancellationToken);
        if (stageExecution is not null)
        {
            stageExecution.Status = StageExecutionStatus.Failed;
            stageExecution.FailedOnUtc = failedOnUtc;
            stageExecution.ErrorSummary = reason;
            await _stageRepository.Update(stageExecution, cancellationToken);
        }

        instance.Status = OrchestrationInstanceStatus.Failed;
        instance.FailedOnUtc = failedOnUtc;
        instance.WaitingSinceUtc = null;
        instance.ErrorSummary = reason;
        instance.LastUpdatedOnUtc = failedOnUtc;
        await _instanceRepository.Update(instance, cancellationToken);

        await WriteTaskFailureTransitions(instance, stageExecution, taskExecution, attempt, reason, cancellationToken);
    }

    private async Task MarkDispatchSent(RuntimeDispatchEnvelope envelope, MessagingDispatchResult result, CancellationToken cancellationToken)
    {
        var dispatch = await TryGetDispatch(envelope, cancellationToken);
        if (dispatch is not null)
        {
            dispatch.DispatchStatus = string.IsNullOrWhiteSpace(result.Status) ? "Dispatched" : result.Status;
            dispatch.SentOnUtc = DateTime.UtcNow;
            dispatch.AcknowledgedOnUtc = dispatch.SentOnUtc;
            dispatch.FailureReason = null;
            dispatch.Metadata["externalReference"] = result.ExternalReference ?? string.Empty;
            await _dispatchRepository.Update(dispatch, cancellationToken);
        }

        await PublishDispatchEvent(envelope, dispatch, RuntimeReactiveEventNames.DispatchPublished, "DispatchPublished", "Scheduled", result.Status, result.ExternalReference, cancellationToken);
        await MarkCompensationCompleted(envelope, result, cancellationToken);
    }

    private async Task MarkDispatchFailed(RuntimeDispatchEnvelope envelope, MessagingDispatchResult result, CancellationToken cancellationToken)
    {
        var dispatch = await TryGetDispatch(envelope, cancellationToken);
        if (dispatch is not null)
        {
            dispatch.DispatchStatus = "Failed";
            dispatch.FailedOnUtc = DateTime.UtcNow;
            dispatch.FailureReason = result.FailureReason;
            dispatch.Metadata["externalReference"] = result.ExternalReference ?? string.Empty;
            await _dispatchRepository.Update(dispatch, cancellationToken);
        }

        await PublishDispatchEvent(envelope, dispatch, RuntimeReactiveEventNames.DispatchFailed, "DispatchFailed", "Scheduled", "Failed", result.ExternalReference, cancellationToken);
        await MarkCompensationFailed(envelope, result, cancellationToken);
    }

    private async Task PublishDispatchEvent(
        RuntimeDispatchEnvelope envelope,
        Abstractions.Runtime.TaskDispatch dispatch,
        string eventName,
        string transitionType,
        string fromStatus,
        string toStatus,
        string externalReference,
        CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(envelope.OrchestrationInstanceId, out var instanceId))
            return;

        var eventId = Id.New();
        var payload = new JsonObject
        {
            ["dispatchId"] = envelope.DispatchId,
            ["destination"] = envelope.Destination?.Address ?? string.Empty,
            ["transport"] = envelope.Destination?.Kind.ToString() ?? string.Empty,
            ["externalReference"] = externalReference ?? string.Empty
        };

        try
        {
            await _reactiveEventPublisher.Publish(new RuntimeReactiveEvent
            {
                Id = eventId,
                EventName = eventName,
                TransitionType = transitionType,
                EnvironmentKey = envelope.EnvironmentKey,
                OrchestrationDefinitionKey = envelope.OrchestrationName,
                OrchestrationInstanceId = new Id(instanceId),
                CorrelationId = envelope.CorrelationId,
                ExecutionKey = envelope.ExecutionKey ?? envelope.DispatchId,
                TaskExecutionId = TryParseId(envelope.TaskExecutionId),
                TaskKey = envelope.TaskKey,
                TaskExecutionAttemptId = dispatch?.TaskExecutionAttemptId,
                FromStatus = fromStatus,
                ToStatus = string.IsNullOrWhiteSpace(toStatus) ? transitionType : toStatus,
                InstanceStatus = "Running",
                OccurredOnUtc = DateTime.UtcNow,
                Message = transitionType,
                Payload = payload,
                ProducedBy = "Krackend.Sagas.Orchestrations.Engine"
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Dispatch state is persisted; live telemetry must not break durable outbox execution.
        }
    }

    private async Task MarkCompensationCompleted(RuntimeDispatchEnvelope envelope, MessagingDispatchResult result, CancellationToken cancellationToken)
    {
        var compensation = await TryGetCompensation(envelope, cancellationToken);
        if (compensation is null)
            return;

        if (!string.Equals(compensation.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(compensation.Status, "Started", StringComparison.OrdinalIgnoreCase))
            return;

        compensation.Status = "Completed";
        compensation.CompletedOnUtc = DateTime.UtcNow;
        compensation.FailedOnUtc = null;
        compensation.ErrorMessage = null;
        compensation.Metadata["dispatchStatus"] = string.IsNullOrWhiteSpace(result.Status) ? "Dispatched" : result.Status;
        compensation.Metadata["externalReference"] = result.ExternalReference ?? string.Empty;
        compensation.ResponsePayload = new JsonObject
        {
            ["dispatchStatus"] = compensation.Metadata["dispatchStatus"]?.DeepClone(),
            ["externalReference"] = compensation.Metadata["externalReference"]?.DeepClone()
        };
        await _compensationRepository.Update(compensation, cancellationToken);

        var instance = await TryGetInstance(envelope, cancellationToken);
        if (instance is null)
            return;

        await WriteInstanceTransition(instance, "CompensationCompleted", "Started", compensation.Status, BuildCompensationPayload(compensation), cancellationToken);
        await CompleteInstanceIfAllCompensationsFinished(instance, compensation, cancellationToken);
    }

    private async Task MarkCompensationFailed(RuntimeDispatchEnvelope envelope, MessagingDispatchResult result, CancellationToken cancellationToken)
    {
        var compensation = await TryGetCompensation(envelope, cancellationToken);
        if (compensation is null)
            return;

        compensation.Status = "Failed";
        compensation.FailedOnUtc = DateTime.UtcNow;
        compensation.ErrorMessage = string.IsNullOrWhiteSpace(result.FailureReason) ? "Compensation dispatch failed." : result.FailureReason;
        compensation.Metadata["dispatchStatus"] = "Failed";
        compensation.Metadata["externalReference"] = result.ExternalReference ?? string.Empty;
        await _compensationRepository.Update(compensation, cancellationToken);

        var instance = await TryGetInstance(envelope, cancellationToken);
        if (instance is null)
            return;

        await WriteInstanceTransition(instance, "CompensationFailed", "Started", compensation.Status, BuildCompensationPayload(compensation), cancellationToken);
        await FailInstance(instance, compensation.ErrorMessage, cancellationToken);
    }

    private async Task CompleteInstanceIfAllCompensationsFinished(
        OrchestrationInstance instance,
        CompensationExecution currentCompensation,
        CancellationToken cancellationToken)
    {
        var compensations = await _compensationRepository.GetByInstanceId(instance.Id, cancellationToken);
        compensations = compensations
            .Where(x => x.Id != currentCompensation.Id)
            .Append(currentCompensation)
            .ToArray();

        if (compensations.Any(x =>
                string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Started", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Unsupported", StringComparison.OrdinalIgnoreCase)))
            return;

        instance.Status = OrchestrationInstanceStatus.Compensated;
        instance.CompensatedOnUtc = DateTime.UtcNow;
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        instance.FinalOutcome = "Compensated";
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteInstanceTransition(
            instance,
            "InstanceCompensated",
            OrchestrationInstanceStatus.Compensating.ToString(),
            instance.Status.ToString(),
            null,
            cancellationToken);
    }

    private async Task FailInstance(OrchestrationInstance instance, string reason, CancellationToken cancellationToken)
    {
        instance.Status = OrchestrationInstanceStatus.Failed;
        instance.FailedOnUtc = DateTime.UtcNow;
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        instance.ErrorSummary = string.IsNullOrWhiteSpace(reason) ? "Compensation dispatch failed." : reason;
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteInstanceTransition(
            instance,
            "InstanceFailed",
            OrchestrationInstanceStatus.Compensating.ToString(),
            instance.Status.ToString(),
            null,
            cancellationToken);
    }

    private async Task WriteInstanceTransition(
        OrchestrationInstance instance,
        string transitionType,
        string fromStatus,
        string toStatus,
        JsonNode payload,
        CancellationToken cancellationToken)
    {
        var transition = new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            TransitionType = transitionType,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            OccurredOnUtc = DateTime.UtcNow,
            Message = transitionType,
            Payload = payload?.DeepClone(),
            ProducedBy = "Krackend.Sagas.Orchestrations.Engine"
        };

        await _transitionRepository.Create(transition, cancellationToken);
        try
        {
            await _reactiveEventPublisher.Publish(new RuntimeReactiveEvent
            {
                Id = transition.Id,
                EventName = ResolveReactiveEventName(transitionType),
                TransitionType = transitionType,
                EnvironmentKey = instance.EnvironmentKey,
                OrchestrationDefinitionKey = instance.OrchestrationDefinitionKey,
                OrchestrationInstanceId = instance.Id,
                CorrelationId = instance.CorrelationId,
                ExecutionKey = instance.ExecutionKey,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                InstanceStatus = instance.Status.ToString(),
                OccurredOnUtc = transition.OccurredOnUtc,
                Message = transition.Message,
                Payload = transition.Payload?.DeepClone(),
                ProducedBy = transition.ProducedBy
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Durable dispatch already updated persisted state; live observers cannot break the outbox action.
        }
    }

    private async Task<CompensationExecution> TryGetCompensation(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!TryReadString(envelope.Metadata, "compensationExecutionId", out var compensationExecutionId) ||
            !Ulid.TryParse(compensationExecutionId, out var compensationUlid))
            return null;

        return await _compensationRepository.TryGetById(new Id(compensationUlid), cancellationToken);
    }

    private async Task<OrchestrationInstance> TryGetInstance(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(envelope.OrchestrationInstanceId, out var instanceUlid))
            return null;

        return await _instanceRepository.GetById(new Id(instanceUlid), cancellationToken);
    }

    private async Task<Abstractions.Runtime.TaskDispatch> TryGetDispatch(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(envelope.DispatchId, out var dispatchUlid))
            return null;

        return await _dispatchRepository.TryGetById(new Id(dispatchUlid), cancellationToken);
    }

    private async Task<TaskExecution> TryGetTaskExecution(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(envelope.TaskExecutionId, out var taskUlid))
            return null;

        return await _taskRepository.GetById(new Id(taskUlid), cancellationToken);
    }

    private async Task<TaskExecutionAttempt> TryGetAttempt(TaskExecution taskExecution, Abstractions.Runtime.TaskDispatch dispatch, CancellationToken cancellationToken)
    {
        var attempt = await _attemptRepository.GetByDispatchId(dispatch.Id, cancellationToken);
        if (attempt is not null)
            return attempt;

        return await _attemptRepository.GetById(dispatch.TaskExecutionAttemptId, cancellationToken);
    }

    private async Task<StageExecution> TryGetStageExecution(TaskExecution taskExecution, CancellationToken cancellationToken)
        => await _stageRepository.GetById(taskExecution.StageExecutionId, cancellationToken);

    private async Task WriteTaskFailureTransitions(
        OrchestrationInstance instance,
        StageExecution stageExecution,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        string reason,
        CancellationToken cancellationToken)
    {
        await WriteRuntimeTransition(
            instance,
            stageExecution,
            taskExecution,
            attempt,
            "TaskFailed",
            TaskExecutionStatus.WaitingResponse.ToString(),
            TaskExecutionStatus.Failed.ToString(),
            BuildFailurePayload(reason),
            cancellationToken);

        if (stageExecution is not null)
        {
            await WriteRuntimeTransition(
                instance,
                stageExecution,
                taskExecution,
                null,
                "StageFailed",
                StageExecutionStatus.Running.ToString(),
                StageExecutionStatus.Failed.ToString(),
                BuildFailurePayload(reason),
                cancellationToken);
        }

        await WriteRuntimeTransition(
            instance,
            stageExecution,
            null,
            null,
            "InstanceFailed",
            OrchestrationInstanceStatus.Waiting.ToString(),
            OrchestrationInstanceStatus.Failed.ToString(),
            BuildFailurePayload(reason),
            cancellationToken);
    }

    private async Task WriteRuntimeTransition(
        OrchestrationInstance instance,
        StageExecution stageExecution,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt,
        string transitionType,
        string fromStatus,
        string toStatus,
        JsonNode payload,
        CancellationToken cancellationToken)
    {
        var transition = new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = stageExecution?.Id,
            TaskExecutionId = taskExecution?.Id,
            TaskExecutionAttemptId = attempt?.Id,
            TransitionType = transitionType,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            OccurredOnUtc = DateTime.UtcNow,
            Message = transitionType,
            Payload = payload?.DeepClone(),
            ProducedBy = "Krackend.Sagas.Orchestrations.Engine"
        };

        await _transitionRepository.Create(transition, cancellationToken);
        try
        {
            await _reactiveEventPublisher.Publish(new RuntimeReactiveEvent
            {
                Id = transition.Id,
                EventName = ResolveReactiveEventName(transitionType),
                TransitionType = transitionType,
                EnvironmentKey = instance.EnvironmentKey,
                OrchestrationDefinitionKey = instance.OrchestrationDefinitionKey,
                OrchestrationInstanceId = instance.Id,
                CorrelationId = instance.CorrelationId,
                ExecutionKey = instance.ExecutionKey,
                StageExecutionId = stageExecution?.Id,
                StageKey = stageExecution?.StageKey,
                TaskExecutionId = taskExecution?.Id,
                TaskKey = taskExecution?.TaskKey,
                TaskExecutionAttemptId = attempt?.Id,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                InstanceStatus = instance.Status.ToString(),
                OccurredOnUtc = transition.OccurredOnUtc,
                Message = transition.Message,
                Payload = transition.Payload?.DeepClone(),
                ProducedBy = transition.ProducedBy
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Durable dispatch state is already persisted; live observers cannot break failure handling.
        }
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, JsonNode> metadata,
        string key,
        out string value)
    {
        value = null;
        if (!metadata.TryGetValue(key, out var node) ||
            node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
            return false;

        value = text;
        return true;
    }

    private static Id? TryParseId(string id)
        => Ulid.TryParse(id, out var ulid) ? new Id(ulid) : null;

    private static JsonObject BuildCompensationPayload(CompensationExecution compensation)
    {
        var payload = compensation.Metadata.ToJsonObject();
        payload["compensationExecutionId"] = compensation.Id.ToString();
        payload["sourceTaskExecutionId"] = compensation.SourceTaskExecutionId.ToString();
        payload["compensationTaskKey"] = compensation.CompensationTaskKey;
        payload["status"] = compensation.Status;
        payload["errorMessage"] = compensation.ErrorMessage ?? string.Empty;
        return payload;
    }

    private static JsonObject BuildFailurePayload(string reason)
        => new()
        {
            ["failureReason"] = reason ?? string.Empty,
            ["errorCode"] = "UnsupportedDispatchTransport"
        };

    private static string ResolveReactiveEventName(string transitionType)
        => transitionType switch
        {
            "CompensationCompleted" => RuntimeReactiveEventNames.CompensationCompleted,
            "CompensationFailed" => RuntimeReactiveEventNames.CompensationFailed,
            "InstanceCompensated" => RuntimeReactiveEventNames.OrchestrationCompensated,
            "InstanceFailed" => RuntimeReactiveEventNames.OrchestrationFailed,
            _ => RuntimeReactiveEventNames.TransitionRecorded
        };

    private static MessagingDispatchCommand CreateCommand(RuntimeDispatchEnvelope envelope)
        => new()
        {
            CommandId = envelope.ExecutionKey ?? envelope.DispatchId,
            CorrelationId = envelope.CorrelationId,
            Destination = envelope.Destination.Address,
            MessageVersion = string.IsNullOrWhiteSpace(envelope.Destination.Version) ? "1.0.0" : envelope.Destination.Version,
            Payload = envelope.Payload.DeepClone(),
            OrchestrationDefinitionKey = envelope.OrchestrationName,
            OrchestrationVersion = envelope.OrchestrationVersion,
            OrchestrationInstanceId = envelope.OrchestrationInstanceId,
            TaskExecutionId = envelope.TaskExecutionId,
            DispatchId = envelope.DispatchId,
            EnvironmentKey = envelope.EnvironmentKey,
            StageKey = envelope.StageKey,
            TaskKey = envelope.TaskKey,
            CurrentStatus = "Running",
            Attempt = envelope.Attempt,
            StartedOnUtc = envelope.CreatedOnUtc,
            UpdatedOnUtc = DateTime.UtcNow
        };
}
