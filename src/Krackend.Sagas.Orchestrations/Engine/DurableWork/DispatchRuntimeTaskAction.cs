namespace Krackend.Sagas.Orchestrations.Engine.DurableWork;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchRuntimeTaskAction"/> class.
    /// </summary>
    public DispatchRuntimeTaskAction(IMessagingCommandDispatcher messagingDispatcher, ITaskDispatchRepository dispatchRepository)
    {
        _messagingDispatcher = messagingDispatcher ?? throw new ArgumentNullException(nameof(messagingDispatcher));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
    }

    /// <inheritdoc />
    public async ValueTask ExecuteAsync(MuleActionContext<RuntimeDispatchEnvelope> context, CancellationToken cancellationToken)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var envelope = context.Payload;

        if (envelope.Destination?.Kind != RuntimeTransportKind.Message)
            throw new InvalidOperationException($"Runtime dispatch transport '{envelope.Destination?.Kind}' is not supported yet.");

        var result = await _messagingDispatcher.Dispatch(CreateCommand(envelope), cancellationToken);
        if (!result.Succeeded)
        {
            await MarkDispatchFailed(envelope, result, cancellationToken);
            throw new InvalidOperationException(result.FailureReason ?? $"Runtime dispatch failed with status '{result.Status}'.");
        }

        await MarkDispatchSent(envelope, result, cancellationToken);
    }

    private async Task MarkDispatchSent(RuntimeDispatchEnvelope envelope, MessagingDispatchResult result, CancellationToken cancellationToken)
    {
        var dispatch = await TryGetDispatch(envelope, cancellationToken);
        if (dispatch is null)
            return;

        dispatch.DispatchStatus = string.IsNullOrWhiteSpace(result.Status) ? "Dispatched" : result.Status;
        dispatch.SentOnUtc = DateTime.UtcNow;
        dispatch.AcknowledgedOnUtc = dispatch.SentOnUtc;
        dispatch.FailureReason = null;
        dispatch.Metadata["externalReference"] = result.ExternalReference ?? string.Empty;
        await _dispatchRepository.Update(dispatch, cancellationToken);
    }

    private async Task MarkDispatchFailed(RuntimeDispatchEnvelope envelope, MessagingDispatchResult result, CancellationToken cancellationToken)
    {
        var dispatch = await TryGetDispatch(envelope, cancellationToken);
        if (dispatch is null)
            return;

        dispatch.DispatchStatus = "Failed";
        dispatch.FailedOnUtc = DateTime.UtcNow;
        dispatch.FailureReason = result.FailureReason;
        dispatch.Metadata["externalReference"] = result.ExternalReference ?? string.Empty;
        await _dispatchRepository.Update(dispatch, cancellationToken);
    }

    private async Task<Abstractions.Runtime.TaskDispatch> TryGetDispatch(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(envelope.DispatchId, out var dispatchUlid))
            return null;

        return await _dispatchRepository.TryGetById(new Id(dispatchUlid), cancellationToken);
    }

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
