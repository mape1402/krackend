namespace Krackend.Sagas.Orchestrations.Engine.DurableWork;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Mule;

/// <summary>
/// Mule outbox action that publishes one runtime dispatch through the configured transport.
/// </summary>
[MuleAction("krackend.runtime.dispatch-task")]
public sealed class DispatchRuntimeTaskAction : IMuleAction<RuntimeDispatchEnvelope>
{
    private readonly IMessagingCommandDispatcher _messagingDispatcher;
    private readonly ITaskDispatchRepository _dispatchRepository;
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
        ICompensationExecutionRepository compensationRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeReactiveEventPublisher reactiveEventPublisher)
    {
        _messagingDispatcher = messagingDispatcher ?? throw new ArgumentNullException(nameof(messagingDispatcher));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
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

        var envelope = context.Payload ?? throw new InvalidOperationException("Runtime dispatch envelope is required.");
        if (envelope.Destination?.Kind != RuntimeTransportKind.Message)
            throw new NotSupportedException($"Runtime dispatch transport '{envelope.Destination?.Kind}' is not supported by the messaging outbox action.");

        var result = await _messagingDispatcher.Dispatch(CreateCommand(envelope), cancellationToken);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.FailureReason ?? $"Runtime dispatch failed with status '{result.Status}'.");

        await MarkDispatchSent(envelope, result, cancellationToken);
        await CompleteCompensationIfNeeded(envelope, result, cancellationToken);
        await PublishDispatchEvent(envelope, result, cancellationToken);
    }

    private async Task MarkDispatchSent(
        RuntimeDispatchEnvelope envelope,
        MessagingDispatchResult result,
        CancellationToken cancellationToken)
    {
        var dispatchId = ParseOptionalId(envelope.DispatchId);
        if (dispatchId is null)
            return;

        await _dispatchRepository.MarkSent(
            dispatchId.Value,
            string.IsNullOrWhiteSpace(result.Status) ? "Published" : result.Status,
            DateTime.UtcNow,
            result.ExternalReference,
            cancellationToken);
    }

    private async Task CompleteCompensationIfNeeded(
        RuntimeDispatchEnvelope envelope,
        MessagingDispatchResult result,
        CancellationToken cancellationToken)
    {
        var compensationId = ReadMetadataId(envelope.Metadata, "compensationExecutionId");
        if (compensationId is null)
            return;

        var compensation = await _compensationRepository.TryGetById(compensationId.Value, cancellationToken);
        if (compensation is null)
            return;

        if (!string.Equals(compensation.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(compensation.Status, "Started", StringComparison.OrdinalIgnoreCase))
            return;

        compensation.Status = "Completed";
        compensation.CompletedOnUtc = DateTime.UtcNow;
        compensation.FailedOnUtc = null;
        compensation.ErrorMessage = null;
        compensation.Metadata["dispatchStatus"] = string.IsNullOrWhiteSpace(result.Status) ? "Published" : result.Status;
        compensation.Metadata["externalReference"] = result.ExternalReference ?? string.Empty;
        compensation.ResponsePayload = new JsonObject
        {
            ["dispatchStatus"] = compensation.Metadata["dispatchStatus"]?.DeepClone(),
            ["externalReference"] = compensation.Metadata["externalReference"]?.DeepClone()
        };
        await _compensationRepository.Update(compensation, cancellationToken);

        var instanceId = ParseOptionalId(envelope.OrchestrationInstanceId);
        if (instanceId is null)
            return;

        var instance = await _instanceRepository.GetById(instanceId.Value, cancellationToken);
        await WriteInstanceTransition(instance, "CompensationCompleted", "Started", "Completed", BuildCompensationPayload(compensation), cancellationToken);
        await CompleteInstanceIfAllCompensationsFinished(instance, compensation, cancellationToken);
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

    private async Task PublishDispatchEvent(
        RuntimeDispatchEnvelope envelope,
        MessagingDispatchResult result,
        CancellationToken cancellationToken)
    {
        var instanceId = ParseOptionalId(envelope.OrchestrationInstanceId);
        if (instanceId is null)
            return;

        await PublishReactiveEvent(new RuntimeReactiveEvent
        {
            Id = Id.New(),
            EventName = RuntimeReactiveEventNames.DispatchPublished,
            TransitionType = "DispatchPublished",
            EnvironmentKey = envelope.EnvironmentKey,
            OrchestrationDefinitionKey = envelope.OrchestrationName,
            OrchestrationInstanceId = instanceId.Value,
            CorrelationId = envelope.CorrelationId,
            ExecutionKey = envelope.ExecutionKey ?? envelope.DispatchId,
            TaskExecutionId = ParseOptionalId(envelope.TaskExecutionId),
            TaskKey = envelope.TaskKey,
            FromStatus = "Scheduled",
            ToStatus = string.IsNullOrWhiteSpace(result.Status) ? "Published" : result.Status,
            InstanceStatus = "Running",
            OccurredOnUtc = DateTime.UtcNow,
            Message = "DispatchPublished",
            Payload = new JsonObject
            {
                ["dispatchId"] = envelope.DispatchId,
                ["destination"] = envelope.Destination?.Address ?? string.Empty,
                ["transport"] = envelope.Destination?.Kind.ToString() ?? string.Empty,
                ["externalReference"] = result.ExternalReference ?? string.Empty
            },
            ProducedBy = "Krackend.Sagas.Orchestrations.Engine"
        }, cancellationToken);
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
        await PublishReactiveEvent(new RuntimeReactiveEvent
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

    private async Task PublishReactiveEvent(RuntimeReactiveEvent message, CancellationToken cancellationToken)
    {
        try
        {
            await _reactiveEventPublisher.Publish(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Realtime diagnostics must not break durable outbox execution.
        }
    }

    private static MessagingDispatchCommand CreateCommand(RuntimeDispatchEnvelope envelope)
        => new()
        {
            CommandId = envelope.ExecutionKey ?? envelope.DispatchId,
            CorrelationId = envelope.CorrelationId,
            Destination = envelope.Destination.Address,
            MessageVersion = string.IsNullOrWhiteSpace(envelope.Destination.Version) ? "1.0.0" : envelope.Destination.Version,
            Payload = envelope.Payload?.DeepClone() ?? new JsonObject(),
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

    private static Id? ParseOptionalId(string id)
        => Ulid.TryParse(id, out var ulid) ? new Id(ulid) : null;

    private static Id? ReadMetadataId(IReadOnlyDictionary<string, JsonNode> metadata, string key)
    {
        if (metadata is null ||
            !metadata.TryGetValue(key, out var node) ||
            node is not JsonValue value ||
            !value.TryGetValue<string>(out var text))
            return null;

        return ParseOptionalId(text);
    }

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

    private static string ResolveReactiveEventName(string transitionType)
        => transitionType switch
        {
            "CompensationCompleted" => RuntimeReactiveEventNames.CompensationCompleted,
            "InstanceCompensated" => RuntimeReactiveEventNames.OrchestrationCompensated,
            _ => RuntimeReactiveEventNames.TransitionRecorded
        };
}
