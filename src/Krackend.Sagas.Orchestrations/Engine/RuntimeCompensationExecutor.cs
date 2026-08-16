using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Executes pending compensations through the runtime dispatcher abstraction.
/// </summary>
internal sealed class RuntimeCompensationExecutor : IRuntimeCompensationExecutor
{
    private const string Pending = "Pending";
    private const string Started = "Started";
    private const string Completed = "Completed";
    private const string Failed = "Failed";
    private const string Scheduled = "Scheduled";
    private const string Unsupported = "Unsupported";

    private readonly ICompensationExecutionRepository _compensationRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly IRuntimeTaskDispatcherResolver _taskDispatcherResolver;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeCompensationExecutor"/> class.
    /// </summary>
    public RuntimeCompensationExecutor(
        ICompensationExecutionRepository compensationRepository,
        IOrchestrationInstanceRepository instanceRepository,
        ITaskExecutionRepository taskRepository,
        IRuntimeTaskDispatcherResolver taskDispatcherResolver,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeReactiveEventPublisher reactiveEventPublisher)
    {
        _compensationRepository = compensationRepository ?? throw new ArgumentNullException(nameof(compensationRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _taskDispatcherResolver = taskDispatcherResolver ?? throw new ArgumentNullException(nameof(taskDispatcherResolver));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _reactiveEventPublisher = reactiveEventPublisher ?? throw new ArgumentNullException(nameof(reactiveEventPublisher));
    }

    /// <inheritdoc/>
    public async Task<RuntimeCompensationExecutionResult> Execute(CompensationExecution compensation, CancellationToken cancellationToken = default)
    {
        if (compensation is null)
            throw new ArgumentNullException(nameof(compensation));

        var isPending = string.Equals(compensation.Status, Pending, StringComparison.OrdinalIgnoreCase);
        var isStarted = string.Equals(compensation.Status, Started, StringComparison.OrdinalIgnoreCase);
        if (!isPending && !isStarted)
            return new RuntimeCompensationExecutionResult(true, compensation.Status, "Compensation is not pending.");

        if (isStarted && IsScheduled(ReadMetadata(compensation, "dispatchStatus")))
            return new RuntimeCompensationExecutionResult(true, compensation.Status, "Compensation dispatch is already scheduled.");

        var instance = await _instanceRepository.GetById(compensation.OrchestrationInstanceId, cancellationToken);
        if (instance.Status == OrchestrationInstanceStatus.Failed)
        {
            instance.Status = OrchestrationInstanceStatus.Compensating;
            instance.CompensationStartedOnUtc ??= DateTime.UtcNow;
            instance.FailedOnUtc = null;
            instance.LastUpdatedOnUtc = DateTime.UtcNow;
            instance.Metadata["compensationRecovery"] = "RecoveredPendingCompensation";
            await _instanceRepository.Update(instance, cancellationToken);
            await WriteTransition(RuntimeTransition.ForInstance(
                instance,
                "InstanceCompensating",
                OrchestrationInstanceStatus.Failed,
                instance.Status), cancellationToken);
        }

        if (instance.Status != OrchestrationInstanceStatus.Compensating)
            return new RuntimeCompensationExecutionResult(true, compensation.Status, "Instance is not compensating.");

        var sourceTask = await _taskRepository.GetById(compensation.SourceTaskExecutionId, cancellationToken);
        var kind = ReadMetadata(compensation, "compensationKind");
        var dispatchType = ReadMetadata(compensation, "dispatchType");
        var destination = ReadMetadata(compensation, "destination");
        var messageVersion = ReadMetadata(compensation, "messageVersion");

        if (!string.Equals(kind, nameof(TaskKind.Messaging), StringComparison.OrdinalIgnoreCase))
            return await MarkUnsupported(instance, sourceTask, compensation, $"Compensation kind '{kind}' is not supported.", cancellationToken);

        if (!string.Equals(dispatchType, nameof(TaskDispatchType.FireAndForget), StringComparison.OrdinalIgnoreCase))
            return await MarkUnsupported(instance, sourceTask, compensation, $"Compensation dispatch type '{dispatchType}' cannot be completed without a compensation response handler.", cancellationToken);

        if (string.IsNullOrWhiteSpace(destination))
            return await MarkUnsupported(instance, sourceTask, compensation, "Compensation destination is required.", cancellationToken);

        var dispatcher = _taskDispatcherResolver.Resolve(kind);
        if (dispatcher is null)
            return await MarkUnsupported(instance, sourceTask, compensation, $"No dispatcher registered for compensation kind '{kind}'.", cancellationToken);

        if (isPending)
        {
            compensation.Status = Started;
            compensation.StartedOnUtc ??= DateTime.UtcNow;
            compensation.Metadata["dispatchId"] = Id.New().ToString();
            compensation.Metadata["commandId"] = $"compensation:{compensation.Id}";
            await _compensationRepository.Update(compensation, cancellationToken);
            await WriteTransition(RuntimeTransition.ForInstancePayload(
                instance,
                "CompensationStarted",
                Pending,
                compensation.Status,
                BuildTransitionPayload(compensation)), cancellationToken);
        }
        else
        {
            compensation.Metadata.TryAdd("dispatchId", Id.New().ToString());
            compensation.Metadata.TryAdd("commandId", $"compensation:{compensation.Id}");
        }

        var result = await dispatcher.Dispatch(new RuntimeTaskDispatchRequest
        {
            CommandId = ReadMetadata(compensation, "commandId"),
            CorrelationId = $"{instance.CorrelationId}:{compensation.CompensationTaskKey}",
            TaskKind = kind,
            DispatchType = dispatchType,
            Destination = destination,
            MessageVersion = string.IsNullOrWhiteSpace(messageVersion) ? "1.0.0" : messageVersion,
            Payload = compensation.RequestPayload?.DeepClone() ?? new JsonObject(),
            OrchestrationDefinitionKey = instance.OrchestrationDefinitionKey,
            OrchestrationVersion = ReadMetadata(compensation, "orchestrationVersion", "1.0.0"),
            OrchestrationInstanceId = instance.Id.ToString(),
            TaskExecutionId = sourceTask.Id.ToString(),
            DispatchId = ReadMetadata(compensation, "dispatchId"),
            EnvironmentKey = instance.EnvironmentKey,
            StageKey = ReadMetadata(compensation, "sourceStageKey"),
            TaskKey = compensation.CompensationTaskKey,
            CurrentStatus = instance.Status.ToString(),
            Attempt = 1,
            StartedOnUtc = compensation.StartedOnUtc ?? DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
            Metadata = new Dictionary<string, JsonNode>
            {
                ["dispatchPurpose"] = "Compensation",
                ["compensationExecutionId"] = compensation.Id.ToString(),
                ["sourceTaskExecutionId"] = compensation.SourceTaskExecutionId.ToString()
            }
        }, cancellationToken);

        compensation.Metadata["dispatchStatus"] = result.Status;
        compensation.Metadata["externalReference"] = result.ExternalReference;
        await WriteTransition(RuntimeTransition.ForInstancePayload(
            instance,
            "CompensationDispatched",
            Started,
            result.Succeeded && !IsScheduled(result.Status) ? Completed : compensation.Status,
            BuildTransitionPayload(compensation)), cancellationToken);

        if (!result.Succeeded)
            return await MarkFailed(instance, sourceTask, compensation, result.FailureReason, cancellationToken);

        if (IsScheduled(result.Status))
        {
            await _compensationRepository.Update(compensation, cancellationToken);
            return new RuntimeCompensationExecutionResult(true, compensation.Status, "Compensation dispatch scheduled.");
        }

        compensation.Status = Completed;
        compensation.CompletedOnUtc = DateTime.UtcNow;
        compensation.ResponsePayload = new JsonObject
        {
            ["dispatchStatus"] = result.Status,
            ["externalReference"] = result.ExternalReference
        };
        await _compensationRepository.Update(compensation, cancellationToken);
        await WriteTransition(RuntimeTransition.ForInstancePayload(
            instance,
            "CompensationCompleted",
            Started,
            compensation.Status,
            BuildTransitionPayload(compensation)), cancellationToken);

        await CompleteInstanceIfAllCompensationsFinished(instance, compensation, cancellationToken);
        return new RuntimeCompensationExecutionResult(true, compensation.Status, "Compensation completed.");
    }

    private async Task<RuntimeCompensationExecutionResult> MarkUnsupported(
        OrchestrationInstance instance,
        TaskExecution sourceTask,
        CompensationExecution compensation,
        string reason,
        CancellationToken cancellationToken)
    {
        compensation.Status = Unsupported;
        compensation.FailedOnUtc = DateTime.UtcNow;
        compensation.ErrorMessage = reason;
        compensation.Metadata["unsupportedReason"] = reason;
        await _compensationRepository.Update(compensation, cancellationToken);
        await WriteTransition(RuntimeTransition.ForInstancePayload(
            instance,
            "CompensationFailed",
            Pending,
            compensation.Status,
            BuildTransitionPayload(compensation)), cancellationToken);

        await FailInstance(instance, sourceTask, reason, cancellationToken);
        return new RuntimeCompensationExecutionResult(false, compensation.Status, reason);
    }

    private async Task<RuntimeCompensationExecutionResult> MarkFailed(
        OrchestrationInstance instance,
        TaskExecution sourceTask,
        CompensationExecution compensation,
        string reason,
        CancellationToken cancellationToken)
    {
        compensation.Status = Failed;
        compensation.FailedOnUtc = DateTime.UtcNow;
        compensation.ErrorMessage = string.IsNullOrWhiteSpace(reason) ? "Compensation dispatch failed." : reason;
        await _compensationRepository.Update(compensation, cancellationToken);
        await WriteTransition(RuntimeTransition.ForInstancePayload(
            instance,
            "CompensationFailed",
            Started,
            compensation.Status,
            BuildTransitionPayload(compensation)), cancellationToken);

        await FailInstance(instance, sourceTask, compensation.ErrorMessage, cancellationToken);
        return new RuntimeCompensationExecutionResult(false, compensation.Status, compensation.ErrorMessage);
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

        if (compensations.Any(x => string.Equals(x.Status, Pending, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(x.Status, Started, StringComparison.OrdinalIgnoreCase)))
            return;

        if (compensations.Any(x => string.Equals(x.Status, Failed, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(x.Status, Unsupported, StringComparison.OrdinalIgnoreCase)))
            return;

        instance.Status = OrchestrationInstanceStatus.Compensated;
        instance.CompensatedOnUtc = DateTime.UtcNow;
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        instance.FinalOutcome = "Compensated";
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForInstance(
            instance,
            "InstanceCompensated",
            OrchestrationInstanceStatus.Compensating,
            instance.Status), cancellationToken);
    }

    private async Task FailInstance(
        OrchestrationInstance instance,
        TaskExecution sourceTask,
        string reason,
        CancellationToken cancellationToken)
    {
        instance.Status = OrchestrationInstanceStatus.Failed;
        instance.FailedOnUtc = DateTime.UtcNow;
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        instance.ErrorSummary = $"Compensation '{sourceTask.TaskKey}' failed: {reason}";
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForInstance(
            instance,
            "InstanceFailed",
            OrchestrationInstanceStatus.Compensating,
            instance.Status), cancellationToken);
    }

    private async Task WriteTransition(RuntimeTransition transition, CancellationToken cancellationToken)
    {
        var executionTransition = new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = transition.Instance.Id,
            StageExecutionId = transition.StageExecution?.Id,
            TaskExecutionId = transition.TaskExecution?.Id,
            TaskExecutionAttemptId = transition.Attempt?.Id,
            TransitionType = transition.Type,
            FromStatus = transition.FromStatus,
            ToStatus = transition.ToStatus,
            OccurredOnUtc = DateTime.UtcNow,
            Message = transition.Type,
            Payload = transition.Payload?.DeepClone(),
            ProducedBy = "Krackend.Sagas.Orchestrations.Engine"
        };

        await _transitionRepository.Create(executionTransition, cancellationToken);
        try
        {
            await _reactiveEventPublisher.Publish(new RuntimeReactiveEvent
            {
                Id = executionTransition.Id,
                EventName = ResolveReactiveEventName(transition.Type),
                TransitionType = transition.Type,
                EnvironmentKey = transition.Instance.EnvironmentKey,
                OrchestrationDefinitionKey = transition.Instance.OrchestrationDefinitionKey,
                OrchestrationInstanceId = transition.Instance.Id,
                CorrelationId = transition.Instance.CorrelationId,
                ExecutionKey = transition.Instance.ExecutionKey,
                StageExecutionId = transition.StageExecution?.Id,
                StageKey = transition.StageExecution?.StageKey,
                TaskExecutionId = transition.TaskExecution?.Id,
                TaskKey = transition.TaskExecution?.TaskKey,
                TaskExecutionAttemptId = transition.Attempt?.Id,
                FromStatus = executionTransition.FromStatus,
                ToStatus = executionTransition.ToStatus,
                InstanceStatus = transition.Instance.Status.ToString(),
                OccurredOnUtc = executionTransition.OccurredOnUtc,
                Message = executionTransition.Message,
                Payload = executionTransition.Payload?.DeepClone(),
                ProducedBy = executionTransition.ProducedBy
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Compensation state is already persisted; live observer outages must not fail recovery processing.
        }
    }

    private static string ResolveReactiveEventName(string transitionType)
        => transitionType switch
        {
            "CompensationStarted" => RuntimeReactiveEventNames.CompensationStarted,
            "CompensationDispatched" => RuntimeReactiveEventNames.CompensationDispatched,
            "CompensationCompleted" => RuntimeReactiveEventNames.CompensationCompleted,
            "CompensationFailed" => RuntimeReactiveEventNames.CompensationFailed,
            "InstanceCompensated" => RuntimeReactiveEventNames.OrchestrationCompensated,
            "InstanceFailed" => RuntimeReactiveEventNames.OrchestrationFailed,
            _ => RuntimeReactiveEventNames.TransitionRecorded
        };

    private static JsonObject BuildTransitionPayload(CompensationExecution compensation)
    {
        var payload = compensation.Metadata.ToJsonObject();
        payload["compensationExecutionId"] = compensation.Id.ToString();
        payload["sourceTaskExecutionId"] = compensation.SourceTaskExecutionId.ToString();
        payload["compensationTaskKey"] = compensation.CompensationTaskKey;
        payload["status"] = compensation.Status;
        payload["errorMessage"] = compensation.ErrorMessage ?? string.Empty;
        return payload;
    }

    private static string ReadMetadata(CompensationExecution compensation, string key, string defaultValue = "")
    {
        if (compensation.Metadata.TryGetValue(key, out var node) &&
            node is JsonValue value &&
            value.TryGetValue<string>(out var text))
            return text ?? defaultValue;

        return defaultValue;
    }

    private static bool IsScheduled(string status)
        => string.Equals(status, Scheduled, StringComparison.OrdinalIgnoreCase);
}
